# Contexto de build: ./frontend
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY MotorLink.Web/*.csproj MotorLink.Web/
RUN dotnet restore MotorLink.Web/MotorLink.Web.csproj
COPY . .
RUN dotnet publish MotorLink.Web/MotorLink.Web.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
# Directorios de sesiones y claves: volúmenes con propietario no-root
RUN mkdir -p /app/data/sessions /app/data/keys && chown -R $APP_UID /app/data
ENV ASPNETCORE_URLS=http://+:8080 \
    Sessions__Path=/app/data/sessions \
    DataProtection__KeysPath=/app/data/keys
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "MotorLink.Web.dll"]
