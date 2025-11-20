using APIUsuarios.Application;
using APIUsuarios.Domain;
using APIUsuarios.Infrastructure.Data;
using APIUsuarios.Infrastructure.Services;
using FluentValidation;
using FluentValidation.AspNetCore;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Prometheus;
using Serilog;
using System.Text;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

try
{
    // ATIVA MENSAGENS DETALHADAS DO JWT (com PII)
    IdentityModelEventSource.ShowPII = true;
    Log.Information("Starting Usuarios.Api");

    var builder = WebApplication.CreateBuilder(args);

    // Health checks (prontos para probes)
    builder.Services.AddHealthChecks();

    // Serilog
    builder.Host.UseSerilog((ctx, lc) => lc.ReadFrom.Configuration(ctx.Configuration).Enrich.FromLogContext());

    // MVC
    builder.Services.AddControllers();

    // Swagger + Bearer
    if (builder.Configuration.GetValue("Swagger:Enabled", true))
    {
        builder.Services.AddEndpointsApiExplorer();

        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo { Title = "Usuarios API", Version = "v1" });

            var securityScheme = new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Description = "Informe APENAS o JWT. O prefixo 'Bearer' é adicionado automaticamente.",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            };

            c.AddSecurityDefinition("Bearer", securityScheme);
            c.AddSecurityRequirement(new OpenApiSecurityRequirement { { securityScheme, Array.Empty<string>() } });
        });
    }

    // CORS (apenas DEV)
    builder.Services.AddCors(opt => { opt.AddPolicy("Dev", p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()); });

    // Npgsql timestamp legacy (evita warnings com DateTime)
    AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

    // -------- PostgreSQL (connection string + DbContext)
    static string BuildPostgresConnectionString(IConfiguration cfg)
    {
        // 1) ConnectionStrings:Postgres (recomendado)
        var cs = cfg.GetConnectionString("Postgres");

        if (!string.IsNullOrWhiteSpace(cs))
        {
            return cs;
        }

        // 2) DATABASE_URL / POSTGRES_URL (estilo Render/Heroku: postgres://user:pass@host:port/db)
        var url = Environment.GetEnvironmentVariable("DATABASE_URL") ?? Environment.GetEnvironmentVariable("POSTGRES_URL");

        if (!string.IsNullOrWhiteSpace(url) && url.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase))
        {
            var uri = new Uri(url);
            var userInfo = uri.UserInfo.Split(':');
            var user = Uri.UnescapeDataString(userInfo[0]);
            var pass = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
            var host = uri.Host;
            var port = uri.Port > 0 ? uri.Port : 5432;
            var db = uri.AbsolutePath.Trim('/');
            return $"Host={host};Port={port};Database={db};Username={user};Password={pass};Ssl Mode=Require;Trust Server Certificate=true";
        }

        // 3) fallback local
        return "Host=localhost;Port=5432;Database=usersdb;Username=postgres;Password=postgres;Ssl Mode=Disable";
    }

    var pg = BuildPostgresConnectionString(builder.Configuration);

    builder.Services.AddDbContext<UsersDbContext>(o =>
    {
        o.UseNpgsql(pg, npg => npg.MigrationsAssembly(typeof(UsersDbContext).Assembly.FullName));
        o.UseSnakeCaseNamingConvention(); // opcional
    });

    // CQRS + FluentValidation
    // Registra os handlers localizados no assembly da camada Application
    builder.Services.AddMediatR(typeof(Program).Assembly, typeof(ListUsersQuery).Assembly);

    builder.Services.AddFluentValidationAutoValidation();
    builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly);

    // ===== JWT
    string rawKey = builder.Configuration["Jwt:Key"];
    byte[] keyBytes = rawKey.StartsWith("base64:") ? Convert.FromBase64String(rawKey["base64:".Length..]) : Encoding.UTF8.GetBytes(rawKey);

    if (keyBytes.Length < 32)
    {
        throw new InvalidOperationException("A chave HS256 deve ter no mínimo 256 bits (32 bytes).");
    }

    var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "GamesPlatform";
    var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "games-platform";

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            RequireSignedTokens = true, // <--- CRÍTICO PARA USO COM Symmetric Key
            IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
            ClockSkew = TimeSpan.FromMinutes(30),
            NameClaimType = "sub",
            RoleClaimType = "role"
        };
        
        // Logs úteis para debugging
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                Log.Error(context.Exception, "Falha na autenticação JWT");
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                var claims = string.Join(", ", context.Principal!.Claims.Select(c => $"{c.Type}={c.Value}"));
                Log.Information("Token JWT validado com sucesso: {Claims}", claims);
                return Task.CompletedTask;
            }
        };
    });
    
    builder.Services.AddAuthorization();
    builder.Services.AddScoped<PasswordService>();

    // Injetar mesma chave/opções no JwtTokenService (assina = valida)
    builder.Services.AddSingleton(keyBytes);
    builder.Services.AddSingleton(new JwtOptions { Issuer = jwtIssuer, Audience = jwtAudience });
    builder.Services.AddScoped<JwtTokenService>();

    var app = builder.Build();

    // endpoints de health
    app.MapHealthChecks("/health/live");
    app.MapHealthChecks("/health/ready");

    // Middleware Prometheus (expõe /metrics)
    app.UseHttpMetrics();          // métricas de request/latência
    app.MapMetrics("/metrics");    // endpoint padrão Prometheus

    if (app.Environment.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
    }

    // Swagger UI
    if (builder.Configuration.GetValue("Swagger:Enabled", true))
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "Usuarios API (Code-First)");
            c.SwaggerEndpoint("/docs/usuarios-api-openapi.yaml", "Usuarios API (OpenAPI YAML)");
            c.EnablePersistAuthorization();
        });
    }

    app.UseHttpsRedirection();

    // static files + MIME .yaml
    var contentTypeProvider = new FileExtensionContentTypeProvider();
    contentTypeProvider.Mappings[".yaml"] = "application/yaml";
    contentTypeProvider.Mappings[".yml"] = "application/yaml";
    app.UseStaticFiles(new StaticFileOptions { ContentTypeProvider = contentTypeProvider });

    app.UseRouting();

    if (app.Environment.IsDevelopment())
    {
        app.UseCors("Dev");
    }

    app.UseSerilogRequestLogging();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();

    // garantir pastas
    var webRoot = app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot");
    var docsDir = Path.Combine(webRoot, "docs");
    Directory.CreateDirectory(docsDir);
    Directory.CreateDirectory(Path.Combine(app.Environment.ContentRootPath, "Logs"));

    // Criar/Migrar DB + SEED ADMIN
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();

        // >>> Postgres: use migrations (não EnsureCreated)
        await db.Database.MigrateAsync();

        Log.Information("Postgres conectado: {Conn}", db.Database.GetDbConnection().ConnectionString);

        var cfg = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var pwdSvc = scope.ServiceProvider.GetRequiredService<PasswordService>();

        var adminEmail = (cfg["AdminSeed:Email"] ?? "admin@exemplo.com").Trim().ToLowerInvariant();
        var adminPass = cfg["AdminSeed:Password"] ?? "Admin@123";
        var adminNome = cfg["AdminSeed:Nome"] ?? "Administrador";

        var exists = await db.Users.AsQueryable().AnyAsync(u => u.Email == adminEmail);
        if (!exists)
        {
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = adminEmail,
                Nome = adminNome,
                Role = Role.ADMIN,
                PasswordHash = "TEMP",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            user.PasswordHash = pwdSvc.Hash(user, adminPass);

            await db.Users.AddAsync(user);
            await db.SaveChangesAsync();

            Log.Information("Seed ADMIN criado: {Email}", adminEmail);
        }
        else
        {
            Log.Information("Seed ADMIN já existe: {Email}", adminEmail);
        }
    }

    var yamlPath = Path.Combine(docsDir, "usuarios-api-openapi.yaml");
    Log.Information("WebRootPath: {WebRoot}", webRoot);
    Log.Information("YAML path esperado: {YamlPath}", yamlPath);
    Log.Information("YAML existe? {Exists}", System.IO.File.Exists(yamlPath));
    Log.Information("Dica: no YAML use 'servers: - url: /' para evitar CORS/mixed content no Swagger UI.");
    app.MapGet("/", () => "OK");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}