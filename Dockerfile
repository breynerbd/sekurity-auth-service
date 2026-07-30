# ---------- Etapa 1: build ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copiamos los .csproj primero (para aprovechar el cache de Docker en npm/nuget restore)
COPY AuthService.sln ./
COPY src/AuthService.Api/AuthService.Api.csproj src/AuthService.Api/
COPY src/AuthService.Application/AuthService.Application.csproj src/AuthService.Application/
COPY src/AuthService.Domain/AuthService.Domain.csproj src/AuthService.Domain/
COPY src/AuthService.Persistence/AuthService.Persistence.csproj src/AuthService.Persistence/

RUN dotnet restore AuthService.sln

# Copiamos el resto del código y compilamos
COPY . .
WORKDIR /src/src/AuthService.Api
RUN dotnet publish -c Release -o /app/publish --no-restore

# ---------- Etapa 2: runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Render inyecta la variable PORT en tiempo de ejecución (no en el build),
# por eso usamos "sh -c" para expandirla al arrancar el contenedor.
EXPOSE 10000
ENTRYPOINT ["sh", "-c", "ASPNETCORE_URLS=http://+:${PORT:-10000} dotnet AuthService.Api.dll"]