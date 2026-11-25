# MotorLink

Plataforma web de compraventa y alquiler de vehículos nuevos y usados (incluye vehículos para taxi, delivery y transporte).
**Etapa 1** – base técnica y autenticación. **Etapa 2** – módulos de negocio (catálogos, vehículos, publicaciones, fotos, búsqueda,
vehículos para trabajar, negociación, favoritos, planes SaaS, suscripciones, dashboard y administración). Detalle en [docs/etapa2.md](docs/etapa2.md).
**Etapa 3** – frontend completo (público, marketplace, publicación guiada, panel de usuario, negociación y administración). Detalle en [docs/frontend.md](docs/frontend.md).

## Stack

C# · .NET 10 · ASP.NET Core Web API + MVC/Razor · SQL Server · EF Core (Code First) + ADO.NET (reportes) · ASP.NET Core Identity ·
JWT + Refresh Token · Bootstrap 5 · Swagger/OpenAPI · Serilog · Docker Compose.

## Estructura

```
MotorLink/
├── backend/                       API (Clean Architecture, monolito modular)
│   ├── MotorLink.sln
│   ├── src/
│   │   ├── MotorLink.Domain/          entidades, enums, excepciones (sin dependencias a otras capas)
│   │   ├── MotorLink.Application/     casos de uso, DTOs, contratos (interfaces)
│   │   ├── MotorLink.Infrastructure/  EF Core, Identity, repositorios, JWT, migraciones, seeds, ADO.NET
│   │   └── MotorLink.API/             controladores, middleware, seguridad, composición (ejecutable)
│   └── tests/ MotorLink.UnitTests · MotorLink.IntegrationTests
├── frontend/MotorLink.Web/        MVC + Razor; habla con la API solo por HTTP (IHttpClientFactory)
├── deploy/  docs/  docker-compose.yml  .env.example
```

Dependencias: `Domain ← Application ← Infrastructure`, y `API` compone todo. El frontend **no** referencia ningún proyecto del
backend ni se conecta a SQL Server. Más detalle en [docs/arquitectura.md](docs/arquitectura.md).

## Requisitos

- .NET SDK 10 (`dotnet --version` ≥ 10.0)
- SQL Server (Docker, o una instancia local: Express/Developer/LocalDB)
- Docker Desktop (solo para la opción con contenedores)
- Herramienta EF: `dotnet tool install -g dotnet-ef`

## Opción A – Docker Compose (todo el entorno)

```bash
cp .env.example .env         # edita MSSQL_SA_PASSWORD y JWT_KEY (y opcionalmente ADMIN_EMAIL / ADMIN_PASSWORD)
docker compose up --build
```

| Servicio | URL | Notas |
|---|---|---|
| Web | http://localhost:5090 | frontend MVC |
| API / Swagger | http://localhost:5080/swagger | Swagger solo con `SWAGGER_ENABLED=true`; el puerto solo escucha en 127.0.0.1 |
| SQL Server | — | sin puerto publicado, en la red privada `data` (volumen `mssql_data`) |

La API aplica las migraciones al iniciar (`Database__MigrateOnStartup=true`) y reintenta mientras SQL Server arranca.

## Opción B – Desarrollo local (sin Docker)

Los secretos **no** están en el repositorio; se pasan por variables de entorno (o `dotnet user-secrets`).

```powershell
# PowerShell
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ConnectionStrings__Default = "Server=.\SQLEXPRESS;Database=MotorLinkDB;Trusted_Connection=True;TrustServerCertificate=True"
$env:Jwt__Key = "<cadena aleatoria de 32+ caracteres>"
$env:Database__AdminEmail = "admin@motorlink.local"      # opcional: crea el ADMIN inicial
$env:Database__AdminPassword = "<contraseña segura>"
dotnet run --project backend/src/MotorLink.API --urls http://localhost:5080     # aplica migraciones en Development

# otra terminal
dotnet run --project frontend/MotorLink.Web --urls http://localhost:5090          # Api:BaseUrl por defecto = http://localhost:5080/
```

Swagger: http://localhost:5080/swagger · Salud: `/health/live`, `/health/ready` (comprueba la BD).

### Migraciones

```bash
cd backend
dotnet ef migrations add <Nombre> -p src/MotorLink.Infrastructure -s src/MotorLink.API -o Persistence/Migrations
dotnet ef database update          -p src/MotorLink.Infrastructure -s src/MotorLink.API   # o dejar que la API migre al iniciar
```

## Pruebas

```bash
cd backend
dotnet test MotorLink.sln
```

- **Unitarias (63)**: autenticación (rotación, reutilización, expiración, logout), JWT, autorización por propiedad, máquina de estados de publicaciones, reglas técnicas por tipo de vehículo, normalización/máscara de placa, firmas de imagen, divisiones seguras, enlaces de WhatsApp.
- **Integración (48)**: arrancan la API real contra SQL Server (auth, catálogos, vehículos, fotos, publicaciones, búsqueda, límites SaaS con concurrencia, vencimientos, negociación, dashboard y administración) y crean/eliminan una base temporal `MotorLinkDB_Test_<guid>`.
  Conexión por defecto: `Server=.\SQLEXPRESS;Trusted_Connection=True`. Para otra instancia:
  `MOTORLINK_TEST_CONNECTION="Server=localhost,1433;User Id=sa;Password=...;TrustServerCertificate=True"`.

## API / Swagger / Postman

113 endpoints bajo `/api/v1` documentados en Swagger (`/swagger`). Colección Postman: [docs/MotorLink.postman_collection.json](docs/MotorLink.postman_collection.json)
(se regenera con `node docs/postman/generate-postman.mjs`; ver [docs/etapa2.md](docs/etapa2.md)). Los enums viajan en MAYÚSCULAS (`PENDING_REVIEW`, `WORK_RENT`).

### Endpoints de autenticación y perfil (etapa 1)

| Método | Ruta | Acceso |
|---|---|---|
| POST | `/auth/register` | público (rate limit) |
| POST | `/auth/login` | público (rate limit, bloqueo tras 5 fallos / 15 min) |
| POST | `/auth/refresh` | público (rota el refresh token) |
| POST | `/auth/logout` | público, requiere el refresh token (`allSessions` opcional) |
| GET | `/auth/me` | autenticado |
| POST | `/auth/change-password` | autenticado; revoca todas las sesiones |
| GET / PUT | `/users/profile` | rol USER/ADMIN |
| GET | `/admin/users`, `/admin/reports/users-by-profile-type` | solo ADMIN (demuestra roles y el reporte ADO.NET) |

## Seguridad (resumen)

- JWT HS256 de 15 min (valida firma, emisor, audiencia y expiración); Refresh Token de 7 días, 64 bytes aleatorios, guardado **solo como hash SHA-256**, rotado en cada uso; reutilización ⇒ se revoca toda la familia de la sesión.
- El navegador solo recibe la cookie `MotorLink.Session` (HttpOnly, SameSite=Lax, Secure en producción). Los tokens viven en el servidor (archivos cifrados con Data Protection en un volumen) y el frontend los renueva de forma transparente.
- Antiforgery obligatorio en todos los POST MVC; cabeceras CSP/X-Frame-Options/nosniff; Razor codifica la salida (XSS).
- Fallback de autorización = usuario autenticado; políticas `AdminOnly`/`User`; handler de propiedad de recursos (`ResourceOwnerHandler`).
- Rate limiting por IP en endpoints de autenticación, ProblemDetails centralizado, logging estructurado (Serilog) sin contraseñas ni tokens.
- Sin secretos en el repositorio: `.env.example` solo contiene marcadores.

## Documentación adicional

- [docs/arquitectura.md](docs/arquitectura.md) – capas, modelo de datos y decisiones técnicas
- [docs/ubigeo.md](docs/ubigeo.md) – origen y limitaciones de los datos de ubicación
- [deploy/README.md](deploy/README.md) – notas de despliegue en producción

## Configuración adicional (etapa 2)

| Variable | Uso |
|---|---|
| `Cloudinary__CloudName`, `Cloudinary__ApiKey`, `Cloudinary__ApiSecret` | Fotografías. Sin ellas la carga de imágenes responde 409 "no configurado" (salvo que se active `ImageStorage__LocalFallback`). |
| `ImageStorage__LocalFallback` (`IMAGE_LOCAL_FALLBACK`) | `true` guarda las fotos en disco (`App_Data/images`, servidas en `/media/*`) cuando Cloudinary no está configurado. Solo desarrollo/demo. |
| `Billing__DemoPaymentsEnabled` | `true` permite pagos **simulados** (no cobran). Por defecto `false` (solo en Development queda `true`). |
| `Maintenance__IntervalMinutes` | Proceso periódico (vence suscripciones y negociaciones, limpia archivos huérfanos). `0` lo desactiva. |
| `RateLimiting__PublicWritePermitLimit` | Límite por minuto/IP para vistas y contactos públicos (60). |

Para actualizar una base existente de la etapa 1 basta con ejecutar la API en Development o `dotnet ef database update` (migración `Stage2Marketplace`).
