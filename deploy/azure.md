# Azure SQL Database + Azure App Service (Linux, .NET 10)

Este documento **no contiene credenciales**. La contraseña de SQL se entrega por separado y nunca se guarda en el repositorio.

## Conexión

Servidor `<SERVIDOR>.database.windows.net`, puerto 1433, base `<BASE>`, usuario `<USUARIO>`, Encrypt=True,
TrustServerCertificate=False, Connection Timeout=60. La API ya usa `UseSqlServer` con `EnableRetryOnFailure` (5 reintentos, espera máx. 10 s)
y `CommandTimeout` de 60 s. Las transacciones manuales (`ExecuteInTransactionAsync`, alta de usuario) se ejecutan dentro de la
estrategia de ejecución, como exige EF Core al activar reintentos.

### Desarrollo: User Secrets (el proyecto API ya tiene `UserSecretsId`)

```powershell
cd backend/src/MotorLink.API
dotnet user-secrets set "ConnectionStrings:Default" "Server=tcp:<SERVIDOR>.database.windows.net,1433;Database=<BASE>;User ID=<USUARIO>;Password=<CONTRASEÑA>;Encrypt=True;TrustServerCertificate=False;Connection Timeout=60;"
dotnet user-secrets set "Jwt:Key" "<cadena aleatoria de 32+ caracteres>"
```

### Producción: variables de entorno (App Service → Configuración → Variables de entorno / Cadenas de conexión)

| Nombre | Valor |
|---|---|
| `ConnectionStrings__Default` (o cadena de conexión tipo *SQLAzure* llamada `Default`) | la misma cadena |
| `Jwt__Key`, `Jwt__Issuer`, `Jwt__Audience` | clave aleatoria ≥ 32 caracteres |
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `Database__MigrateOnStartup` | `false` (las migraciones se aplican de forma controlada, ver abajo) |
| `Database__AdminEmail` / `Database__AdminPassword` | solo para crear el primer ADMIN; retirarlas después |
| `Cloudinary__*`, `Cors__AllowedOrigins__0` (si el frontend vive en otro dominio) | según corresponda |

Preferible: guardar los secretos en Azure Key Vault y referenciarlos desde App Service (`@Microsoft.KeyVault(...)`).

## Migraciones sobre Azure SQL

Modelo y migraciones revisados (`InitialCreate`, `Stage2Marketplace`; `has-pending-model-changes` = sin cambios). Ninguna migración
elimina tablas ni la base. Datos iniciales incluidos en las migraciones: roles, cinco planes de suscripción, marcas, modelos, tipos de
vehículo, categorías y ubigeo. El primer ADMIN se crea con `Database__AdminEmail/Password`.

1. Firewall de Azure SQL: permitir tu IP (o "Allow Azure services" para App Service).
2. Aplicar con EF (usa el secreto de usuario o la variable de entorno):

```powershell
$env:ConnectionStrings__Default = "<cadena con la contraseña>"      # solo en esta sesión de terminal
dotnet ef database update -p backend/src/MotorLink.Infrastructure -s backend/src/MotorLink.API
```

   Alternativa auditable: revisar y ejecutar `deploy/migrations.idempotent.sql` (generado con `dotnet ef migrations script --idempotent`).

Comprobación: `GET /health` → `{"status":"Healthy"}`; `/health/live` (proceso) y `/health/ready` (base de datos).

## Despliegue del backend en App Service

- Plan Linux, runtime **.NET 10**. App Service escucha en el puerto 8080 por defecto; no hace falta configurar `ASPNETCORE_URLS`.
- Publicar: `dotnet publish backend/src/MotorLink.API -c Release -o publish` y desplegar la carpeta (zip deploy / GitHub Actions),
  o desplegar la imagen del `backend/Dockerfile`.
- Health check de App Service: ruta `/health`.
- Activar "Solo HTTPS". TLS termina en el front de Azure; la API ya acepta `X-Forwarded-*`.
- Fotos: configurar Cloudinary. `ImageStorage__LocalFallback` debe quedar en `false` (el disco de App Service no es duradero).
- El frontend (`frontend/MotorLink.Web`) es otra aplicación: se despliega aparte y solo necesita `Api__BaseUrl` apuntando a la API.
