# Etapa 3 – Frontend

ASP.NET Core MVC/Razor + Bootstrap 5 + CSS propio con variables + JavaScript modular (ES modules) + Chart.js + Lucide.
Sin React/Angular/Vue. El navegador **nunca** habla con la API ni guarda tokens: el sitio web actúa de intermediario.

## Ejecutar

```bash
dotnet run --project backend/src/MotorLink.API --urls http://localhost:5080     # ver README (variables)
dotnet run --project frontend/MotorLink.Web --urls http://localhost:5090
```

Datos de demostración (solo por la API pública, nada directo a la BD):

```bash
dotnet run --project tools/DemoSeeder -- http://localhost:5080
```

Cuentas demo: `admin@motorlink.local` (definida con `Database__AdminEmail/Password`), `ventas@autoperu.demo.motorlink.example`
(plan Profesional), `rosa.quispe@demo.motorlink.example` (plan Gratis), `carlos.mendoza@demo.motorlink.example` (usuario normal).
Las contraseñas las imprime el seeder; no son credenciales reales.

## Seguridad

- Cookie `MotorLink.Session` (HttpOnly, SameSite=Lax, Secure en producción); tokens en archivos cifrados con Data Protection en el servidor.
- Renovación del access token serializada (`BearerTokenHandler` + `TokenProvider`); 401 → login con `returnUrl`.
- Antiforgery global (`AutoValidateAntiforgeryToken`); las llamadas `fetch` envían `X-CSRF-TOKEN`.
- CSP estricta: `script-src 'self'`, `style-src 'self'` (sin scripts, estilos ni manejadores en línea).
- La placa completa no se muestra públicamente; no se recogen datos de tarjeta (los pagos son simulados y así se indica).

## Pantallas

| Zona | Rutas |
|---|---|
| Público | `/`, `/vehiculos` (búsqueda + filtros), `/vehiculos/{id}` (detalle, sección WORK_RENT), `/planes` |
| Acceso | `/ingresar`, `/registro`, `/salir`, cambio de contraseña, `/acceso-denegado` |
| Panel (`/panel`) | inicio con gráficos, vehículos, publicaciones, **publicar en 5 pasos**, favoritos, negociaciones (recibidas/enviadas/detalle), suscripción, pagos, checkout simulado, perfil, seguridad, notificaciones |
| Admin (`/admin`) | inicio, usuarios, negocios, vehículos, publicaciones, negociaciones, planes, suscripciones, pagos, catálogos, ubicaciones, reportes, auditoría |

## Componentes reutilizables

- ViewComponents: `NotificationBell`, `Pagination`, `ListingCard`. Partials: `_PlanCard`, `_EmptyState`, `_SideNav`, `_Toasts`, `_Brand`.
- JS (`wwwroot/js`): `core/{http,dom}`, `ui/{toast,confirm,loading,icons,modal-fill}`, `features/{forms,favorites,cascade,marketplace,gallery,listing-actions,charts,notifications,wizard}`.
- Tokens de diseño en `wwwroot/css/tokens.css` (enlazados a las variables de Bootstrap).

## Integraciones

Solo endpoints existentes de la API (clientes tipados en `Services/Api`). Para fotos sin Cloudinary la API ofrece un
almacenamiento local de reserva (`ImageStorage:LocalFallback`) servido en `/media/*` y proxeado por el sitio web.
Se añadió además `GET api/v1/admin/{businesses|vehicles|negotiations}` (listados que el panel admin necesitaba).

## Pruebas

```bash
dotnet test backend/MotorLink.sln                  # unitarias + integración de la API
dotnet test frontend/MotorLink.Web.Tests           # formato, validación, sesiones, cabeceras/CSP, antiforgery, degradación sin API
cd frontend/e2e && npm install && node public.mjs && node flows.mjs && node wizard.mjs && node sweep.mjs
```

Las pruebas E2E usan Chrome real (`CHROME_PATH`) contra API y web levantadas con datos demo; ver `frontend/e2e/README.md`.

## Licencias de terceros

Inter (SIL OFL), Lucide (ISC), Chart.js (MIT), Bootstrap (MIT), jQuery y jQuery Validation (MIT). La ilustración del hero es propia.
