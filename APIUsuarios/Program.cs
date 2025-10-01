using System.Text;
using FluentValidation;
using FluentValidation.AspNetCore;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using APIUsuarios.Application;
using APIUsuarios.Infrastructure.Data;
using APIUsuarios.Infrastructure.Services;
using APIUsuarios.Domain;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting Usuarios.Api");

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, lc) => lc
        .ReadFrom.Configuration(ctx.Configuration)
        .Enrich.FromLogContext());

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

    // CORS (DEV)
    builder.Services.AddCors(opt =>
    {
        opt.AddPolicy("Dev", p => p
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod());
    });

    // EF Core — SQLite
    var sqlite = builder.Configuration.GetConnectionString("Sqlite") ?? "Data Source=users.db";
    builder.Services.AddDbContext<UsersDbContext>(o => o.UseSqlite(sqlite));

    // CQRS + FluentValidation
    // MediatR v12+
    // usando a API antiga (v10/v11)
    builder.Services.AddMediatR(
        typeof(Program).Assembly,                          // assembly da API
        typeof(APIUsuarios.Application.ListUsersQuery).Assembly // assembly onde estão os handlers (Application)
    );


    builder.Services.AddFluentValidationAutoValidation();
    builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly);

    // ===== JWT
    string rawKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key não configurado");

    byte[] keyBytes = rawKey.StartsWith("base64:", StringComparison.OrdinalIgnoreCase)
        ? Convert.FromBase64String(rawKey["base64:".Length..])
        : Encoding.UTF8.GetBytes(rawKey);

    if (keyBytes.Length < 32)
        throw new InvalidOperationException($"Jwt:Key muito curta ({keyBytes.Length} bytes). Para HS256 use >= 32 bytes.");

    var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "GamesPlatform";
    var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "games-platform";

    builder.Services
      .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
      .AddJwtBearer(options =>
      {
          options.MapInboundClaims = false;
          options.TokenValidationParameters = new TokenValidationParameters
          {
              ValidateIssuer = true,
              ValidateAudience = true,
              ValidateLifetime = true,
              ValidateIssuerSigningKey = true,
              ValidIssuer = jwtIssuer,
              ValidAudience = jwtAudience,
              IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
              ClockSkew = TimeSpan.FromMinutes(30),
              NameClaimType = "sub",
              RoleClaimType = "role"
          };
          options.Events = new JwtBearerEvents
          {
              OnAuthenticationFailed = ctx => { Serilog.Log.Error(ctx.Exception, "JWT fail"); return Task.CompletedTask; },
              OnChallenge = ctx => { Serilog.Log.Warning("JWT 401: {Err} {Desc}", ctx.Error, ctx.ErrorDescription); return Task.CompletedTask; },
              OnTokenValidated = ctx => { Serilog.Log.Information("JWT ok: {Claims}", string.Join(", ", ctx.Principal!.Claims.Select(c => $"{c.Type}={c.Value}"))); return Task.CompletedTask; }
          };
      });

    // 🔧 FALTAVA no seu código: autorização + PasswordService no DI
    builder.Services.AddAuthorization();                  // <-- adicionado
    builder.Services.AddScoped<PasswordService>();        // <-- adicionado

    // Injetar mesma chave/opções no JwtTokenService (assina = valida)
    builder.Services.AddSingleton(keyBytes);
    builder.Services.AddSingleton(new JwtOptions { Issuer = jwtIssuer, Audience = jwtAudience });
    builder.Services.AddScoped<JwtTokenService>();

    var app = builder.Build();

    if (app.Environment.IsDevelopment())
        app.UseDeveloperExceptionPage();

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
        app.UseCors("Dev");

    app.UseSerilogRequestLogging();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();

    // garantir pastas
    var webRoot = app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot");
    var docsDir = Path.Combine(webRoot, "docs");
    Directory.CreateDirectory(docsDir);
    Directory.CreateDirectory(Path.Combine(app.Environment.ContentRootPath, "Logs"));

    // criar DB + SEED ADMIN
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        await db.Database.EnsureCreatedAsync();
        Log.Information("SQLite file em: {DbPath}", db.Database.GetDbConnection().DataSource);

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
                PasswordHash = "TEMP", // required
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