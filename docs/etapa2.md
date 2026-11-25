# Etapa 2 – módulos de negocio (resumen técnico)

## Módulos y endpoints (`/api/v1`)

| Módulo | Endpoints principales | Acceso |
|---|---|---|
| Catálogos (`CatalogsController`) | `GET catalogs/{vehicle-types,brands,models,departments,provinces,districts}` (filtros `brandId`, `vehicleTypeId`, `departmentId`, `provinceId`) | público (solo activos) |
| Catálogos admin (`AdminCatalogsController`) | `GET/POST/PUT/PATCH {id}/active/DELETE admin/catalogs/{tipo}` ×6 | ADMIN |
| Vehículos (`VehiclesController`) | `POST/GET/GET {id}/PUT/DELETE vehicles`; fotos: `GET/POST vehicles/{id}/images`, `DELETE …/{imageId}`, `PUT …/order`, `PATCH …/{imageId}/cover` | USER (solo propios; ajenos ⇒ 404) |
| Publicaciones (`ListingsController`) | público: `GET listings` (búsqueda), `GET listings/{id}`, `GET …/advertiser`, `POST …/views`, `POST …/contact`; privado: `GET mine`, `GET mine/{id}`, `POST`, `PUT {id}`, `PATCH {id}/status`, `PATCH {id}/featured`, `DELETE {id}` | mixto |
| Favoritos | `GET favorites`, `PUT favorites/{listingId}` (idempotente), `DELETE favorites/{listingId}` | USER |
| Negociaciones | `POST negotiations`, `GET negotiations?role&status`, `GET {id}`, `POST {id}/offers`, `PATCH {id}/accept|reject|cancel` | USER (participantes) |
| Planes / Suscripciones | `GET plans`; `GET subscriptions/current`, `GET subscriptions`, `GET subscriptions/payments`, `POST change-plan`, `POST renew`, `POST {id}/pay`, `PATCH {id}/cancel` | público / USER |
| Dashboard | `GET dashboard?from&to`, `GET dashboard/export` (CSV) | USER |
| Notificaciones | `GET notifications`, `GET unread-count`, `PATCH {id}/read`, `PATCH read-all` | USER |
| Administración | usuarios (`admin/users`, `PATCH …/status`), moderación (`admin/listings`, `POST …/approve|reject`), planes (`admin/plans`), suscripciones y pagos de demostración (`admin/subscriptions`, `admin/payments/demo`), auditoría (`admin/audit-logs`), estadísticas (`admin/stats`), reportes (`admin/reports/*`, CSV y ADO.NET) | ADMIN |

## Reglas clave

**Vehículos.** Validación técnica por tipo (`VehicleSpecValidator`): puertas/asientos/carga/cilindrada; usados exigen placa, nuevos pueden no tenerla (≤ 500 km, 0 propietarios previos). La placa se normaliza (MAYÚSCULAS, solo alfanuméricos) y es única entre vehículos activos (índice filtrado). Marca, modelo y tipo deben ser compatibles (`ModelVehicleTypes`). En endpoints públicos la placa se enmascara (`AB****`) y la titularidad declarada **no se publica**; `OwnershipVerified` es siempre `false` porque no existe verificación real.

**Publicaciones.** `Listing` (estado, anunciante, contacto, ubicación) → `ListingOperation` (SALE / RENT / WORK_RENT, cada una con moneda, precios, garantía, negociable, disponibilidad) → `WorkRentCondition` (solo WORK_RENT: uso permitido, turno, experiencia, licencia, zona, límite de km, quién asume combustible/mantenimiento/seguro, requisitos). Son condiciones **declaradas**: no se valida legalmente al conductor.

Máquina de estados (`ListingStateMachine`):

```
DRAFT → PENDING_REVIEW → PUBLISHED ⇄ PAUSED
            │                ├→ RESERVED ⇄ PUBLISHED / → RENTED / → SOLD
            └→ REJECTED → DRAFT   ├→ RENTED  ⇄ PUBLISHED / → SOLD
 (cualquiera no terminal) → ARCHIVED → DRAFT;  SOLD → ARCHIVED
```
Solo ADMIN aprueba/rechaza. RESERVED/RENTED comprometen una modalidad y la publicación deja de figurar como disponible (también las demás modalidades). SOLD cierra todas las modalidades activas y cancela negociaciones abiertas. Cada cambio queda en `ListingStatusHistory`.

**Búsqueda.** LINQ + EF Core con `AsNoTracking`, proyección a DTO, `COUNT` + `OFFSET/FETCH` en SQL Server y subconsultas correlacionadas (sin N+1). Precio, moneda, negociable, disponibilidad y tipo se evalúan sobre **la misma modalidad**. Filtrar u ordenar por precio exige `currency`. El precio de referencia es el de venta o la tarifa diaria (o su equivalente semanal/mensual). Orden por defecto: destacadas y fecha de publicación.

**Fotografías.** `IImageStorageService` (Cloudinary en Infrastructure). Máx. 5 MB, JPEG/PNG/WebP verificados por firma de bytes (y coincidencia con el tipo declarado). Límite del plan comprobado antes de subir y de nuevo bajo bloqueo transaccional. Fallos: si el registro en BD falla se borra el archivo subido; si el borrado en Cloudinary falla queda en `OrphanedImages` y un proceso periódico reintenta (hasta 10 veces).

**Negociación.** Cada oferta es un registro inmutable (`NegotiationOffer`, con `Sequence`). Estados PENDING → COUNTER_OFFER → ACCEPTED | REJECTED | CANCELLED | EXPIRED (7 días desde la última oferta). Solo la contraparte de la última oferta responde y solo se acepta la **última** (`offerId`). Moneda/periodicidad se heredan de la modalidad y no cambian. Concurrencia: transacción + `RowVersion` (409 ante respuestas simultáneas) + índices únicos (`(NegotiationId, Sequence)` y una negociación abierta por comprador/modalidad). Un acuerdo es de **precio**, no confirma pago ni entrega.

**Planes y límites.** Los 5 planes (precios, límites y beneficios) viven en la tabla `Plans` y se editan en `admin/plans`. `ISubscriptionPermissions` es el único lugar que evalúa beneficios (ningún `if (plan == "PRO")` en el código). Los consumos de cupo (activar publicación, subir foto, destacar) se hacen dentro de una transacción con bloqueo `sp_getapplock` por usuario: 5 envíos simultáneos con plan GRATIS producen exactamente 2 éxitos (probado). El plan gratuito se asigna al registrarse (y de forma perezosa a usuarios previos). Índice único: una suscripción ACTIVE por usuario.

**Vencimientos.** `MaintenanceWorker` → `ExpireDueAsync`: suscripción vencida ⇒ EXPIRED, plan gratuito, `PlanLimitEnforcer` pausa las publicaciones excedentes (conserva las más antiguas; RESERVED/RENTED no se tocan), retira destacados y notifica. No se elimina ningún vehículo, foto ni historial.

**Pagos.** `IPaymentProvider` (futuro Culqi). Hoy solo `DemoPaymentProvider`: no cobra; los pagos quedan con `IsSimulated = true` y la suscripción con `PaymentMode = Simulated`. Las asignaciones administrativas son `Complimentary`. `PaymentMode.Real` está reservado para una pasarela real. `admin/stats` separa `mrrReal`, `mrrSimulated` y `complimentaryValue`.

**Dashboard (fórmulas).** `Views` (sin las del propio anunciante), `ContactClicks` (intenciones, **no** contactos concretados), `ProposalsReceived/Sent` (negociaciones creadas en el rango), `AcceptedNegotiations` (acuerdos de precio con actualización en el rango), `VehiclesSold/Rented` (historial de estados en el rango), `ConversionRate = aceptadas entre las recibidas del rango / recibidas`, `ContactRate = clics / vistas`; ambas devuelven 0 si el denominador es 0. Estado actual (no filtrado por fechas): publicaciones activas, vehículos y consumo del plan. Evolución mensual y exportación CSV dependen del plan (`HasAdvancedStats`, `CanExportReports`).

**ADO.NET.** `PlanRevenueReportService` (`admin/reports/plan-revenue`): dos `SqlCommand` parametrizados llenan un `DataSet` con `SqlDataAdapter`; se define un `DataRelation` Planes→Suscripciones, una columna calculada (`Total * Parent(...).MonthlyPrice`), `DataView`/`Compute` y totales, todo en memoria desconectada. Una prueba verifica que coincide con las estadísticas calculadas con EF. `ReportQueries` (stage 1) conserva el ejemplo con `SqlDataReader`.

## Migraciones

`InitialCreate` (etapa 1) + `Stage2Marketplace` (evoluciona Vehicles/Listings/Negotiations/Plans/Subscriptions/Payments/Notifications, crea `ModelVehicleTypes`, `WorkRentConditions`, `ListingStatusHistory`, `OrphanedImages`, índices únicos filtrados y restricciones CHECK; reemplaza los datos semilla). Verificada aplicándola sobre una base creada con la etapa 1.

## Decisiones y compromisos

- **Application referencia EF Core (abstracciones)** y usa `IApplicationDbContext` en lugar de un repositorio por entidad: reduce código repetitivo; el proveedor SQL Server sigue confinado a Infrastructure. Auth/Perfil conservan sus repositorios específicos.
- **Ids de catálogos** (tipos, marcas, modelos) son claves manuales con semilla; las nuevas filas usan `MAX+1` (la PK evita colisiones). Provincias/distritos usan su código UBIGEO como Id.
- **Un `Listing` por vehículo** con varias modalidades (un índice único filtrado lo garantiza).
- **Enums en BD como texto** (`PascalCase`) y en JSON como `SNAKE_CASE` mayúsculas; los parámetros de query aceptan ambos.
- El perfil empresarial (plan PROFESSIONAL+) se exige para publicar como anunciante BUSINESS; el registro/edición de perfil de la etapa 1 no se restringió.
- Cancelar una suscripción activa detiene la renovación pero conserva el plan hasta su vencimiento; no hay prorrateo al cambiar de plan ni renovación automática (no hay pasarela).

## Pendientes y limitaciones

- **Cloudinary no se probó contra el servicio real** (sin credenciales): las pruebas usan un almacenamiento en memoria que implementa la misma interfaz. Verificar con credenciales reales.
- **Docker no se pudo probar** (Docker Desktop pausado en este equipo): el compose valida (`docker compose config`) pero no se levantó.
- Un access token ya emitido sigue siendo válido hasta 15 min tras bloquear una cuenta (los refresh tokens sí se revocan).
- La búsqueda de texto usa `LIKE` (`Contains`); para grandes volúmenes convendría Full-Text Search.
- Sin verificación real de titularidad ni de documentos; sin envío de correos/SMS (solo notificaciones internas).
- El frontend MVC aún no consume estos módulos (solo autenticación y perfil de la etapa 1).
- Catálogo UBIGEO parcial (ver `ubigeo.md`).
