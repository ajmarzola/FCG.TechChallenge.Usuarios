# ====== STAGE 1: BUILD (.NET SDK) ======
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Estamos DENTRO da pasta do projeto
COPY APIUsuarios.csproj ./
RUN dotnet restore APIUsuarios.csproj

# Copia o restante desta pasta (código fonte)
COPY . .

# Publica em Release
RUN dotnet publish APIUsuarios.csproj -c Release -o /app/publish

# ====== STAGE 2: RUNTIME (ASP.NET 8) ======
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./

# App Service Linux costuma usar porta 8080
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "APIUsuarios.dll"]
