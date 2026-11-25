# Contexto de build: ./backend
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restauración en una capa propia para aprovechar la caché
COPY MotorLink.sln ./
COPY src/MotorLink.Domain/*.csproj src/MotorLink.Domain/
COPY src/MotorLink.Application/*.csproj src/MotorLink.Application/
COPY src/MotorLink.Infrastructure/*.csproj src/MotorLink.Infrastructure/
COPY src/MotorLink.API/*.csproj src/MotorLink.API/
COPY tests/MotorLink.UnitTests/*.csproj tests/MotorLink.UnitTests/
COPY tests/MotorLink.IntegrationTests/*.csproj tests/MotorLink.IntegrationTests/
RUN dotnet restore MotorLink.sln

COPY . .
RUN dotnet publish src/MotorLink.API/MotorLink.API.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
# Carpeta de fotos locales (solo si ImageStorage__LocalFallback=true); se monta como volumen.
RUN mkdir -p /app/App_Data && chown $APP_UID /app/App_Data
USER $APP_UID
ENTRYPOINT ["dotnet", "MotorLink.API.dll"]
