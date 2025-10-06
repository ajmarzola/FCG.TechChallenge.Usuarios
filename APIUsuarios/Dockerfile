# ====== STAGE 1: BUILD (.NET SDK) ======
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copia o .csproj primeiro para aproveitar cache no restore
COPY APIUsuarios.csproj ./
RUN dotnet restore "APIUsuarios.csproj"

# Copia o restante do código
COPY . .

# Publica em Release (sem restaurar de novo) e sem AppHost nativo
RUN dotnet publish "APIUsuarios.csproj" -c Release -o /app/publish --no-restore /p:UseAppHost=false


# ====== STAGE 2: RUNTIME (ASP.NET 8) ======
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Kestrel ouvindo na 8080 (compatível com ECS sem LB)
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "APIUsuarios.dll"]
