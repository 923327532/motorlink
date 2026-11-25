# Arquitectura y decisiones técnicas

## Capas (backend)

| Capa | Contiene | Depende de |
|---|---|---|
| Domain | Entidades, enums, excepciones de negocio, `RefreshToken.Revoke/IsExpired` | nada propio (usa `Microsoft.Extensions.Identity.Stores` para heredar de `IdentityUser<Guid>`) |
| Application | `AuthService`, `UserService`, DTOs con DataAnnotations, interfaces (`IIdentityService`, `ITokenService`, `IRefreshTokenRepository`, `IUserProfileRepository`, `IReportQueries`) | Domain |
| Infrastructure | `AppDbContext`, configuraciones Fluent API, migraciones, seeds, `IdentityService` (UserManager), `TokenService`, repositorios, reportes ADO.NET | Application, Domain |
| API | Controladores, middleware de excepciones, JWT, autorización, rate limiting, Swagger, health checks | todas (composición) |

Los repositorios son específicos (no hay repositorio genérico): solo existen las operaciones que los casos de uso necesitan.

## Modelo de datos (EF Core, Code First)

27 tablas: `Users`, `Roles`, `UserRoles` (Identity adaptado, clave `Guid`), `UserProfiles` (1:1 con Users), `BusinessProfiles`,
`VehicleTypes`, `Brands`, `Models`, `Vehicles`, `VehicleImages`, `Listings`, `ListingOperations`, `Departments`, `Provinces`,
`Districts`, `Favorites`, `Negotiations`, `NegotiationOffers`, `Plans`, `Subscriptions`, `Payments`, `RefreshTokens`,
`Notifications`, `ListingViews`, `ContactEvents`, `AuditLogs` (+ tablas auxiliares de Identity: `UserClaims`, `UserLogins`,
`RoleClaims`, `UserTokens`).

- Cada entidad tiene su `IEntityTypeConfiguration<T>` en `Infrastructure/Persistence/Configurations`.
- Decimales `DECIMAL(18,2)` por convención; todas las fechas en UTC (conversor que marca `DateTimeKind.Utc`).
- `CreatedAt`/`UpdatedAt` los asigna `SaveChanges`; borrado lógico (`IsDeleted`/`DeletedAt`) con filtro global y conversión de `Remove` a marca lógica.
- Enumeraciones guardadas como texto (legibles y estables ante reordenamientos).
- Una publicación puede tener una operación `SALE` y/o `RENT` (`ListingOperations`, único por `(ListingId, OperationType)`); el precio vive en la operación.
- Las FK hacia `Users` son `Restrict` para evitar rutas de cascada múltiples en SQL Server.
- Seeds en `HasData`: roles `ADMIN`/`USER` (GUID fijo), planes (precios **de demostración**), tipos de vehículo, 10 marcas y 36 modelos, 25 departamentos y un subconjunto de provincias/distritos (ver `ubigeo.md`).
- El administrador inicial no se siembra con contraseña en el código: se crea al iniciar si `Database:AdminEmail/AdminPassword` están definidos.

## Autenticación

1. `login/register` → `AccessToken` (JWT, 15 min) + `RefreshToken` (opaco, 7 días). Solo el hash SHA-256 del refresh se persiste.
2. `refresh` marca el token usado como `Rotated` (con enlace a su sustituto) y emite uno nuevo en la misma `FamilyId`.
3. Si llega un token ya revocado se asume robo: se revoca toda la familia (`ReuseDetected`).
4. `logout` (por familia o `allSessions`) y `change-password` (todas las sesiones) revocan tokens. Un access token ya emitido sigue siendo válido hasta expirar (≤ 15 min); es la compensación habitual de usar JWT sin lista de revocación.
5. Bloqueo de cuenta: 5 intentos fallidos ⇒ 15 min (HTTP 423). Rate limiting adicional por IP (10/min por defecto).

## Frontend como intermediario

`AccountController` crea una sesión de servidor (`ISessionStore`) con los tokens y emite una cookie con claims + id de sesión.
`BearerTokenHandler` adjunta el JWT a cada llamada a la API; `TokenProvider` lo renueva (con bloqueo por sesión para no
disparar la detección de reutilización con peticiones paralelas). Si la API rechaza la renovación, la sesión local se elimina y
`OnValidatePrincipal` invalida la cookie. Los clientes HTTP (`AuthApiClient`, `UsersApiClient`) usan `IHttpClientFactory`.

## Decisiones y compromisos

- **Sesiones en archivos cifrados (volumen)** en lugar de una base de datos: el frontend no debe conectarse a SQL Server. Es adecuado para una sola instancia; para escalar horizontalmente habría que sustituir `ISessionStore` (p. ej. Redis) sin tocar el resto.
- **Domain referencia el paquete de Identity** para `IdentityUser<Guid>` (decisión pragmática para no duplicar tablas).
- **DataAnnotations** en lugar de FluentValidation: suficiente para esta etapa y sin dependencias extra.
- **Swagger** con Swashbuckle (el generador integrado de .NET 10 no incluye UI).
- **Datos de ubicación parciales** (ver `ubigeo.md`).
