# Pruebas E2E (navegador real)

Requisitos: API en `http://localhost:5080` (Development, con `RateLimiting__AuthPermitLimit` alto), frontend en `http://localhost:5090`,
datos de demostración sembrados (`dotnet run --project ../../tools/DemoSeeder`) y Google Chrome (`CHROME_PATH` si no está en la ruta por defecto).

```bash
npm install
node public.mjs      # 9 páginas públicas × 3 tamaños (escritorio/tablet/móvil): consola, imágenes rotas, desborde horizontal
node flows.mjs       # registro, login, antiforgery, favoritos, propuesta, contrapropuesta y acuerdo entre dos usuarios  (TARGET=Mazda para otra publicación)
node wizard.mjs      # asistente de 5 pasos, subida/orden/portada/borrado de fotos, límite del plan, moderación del administrador
node sweep.mjs       # todas las pantallas del panel y de administración (escritorio y móvil) + protección de rutas
```
Las capturas se guardan en `./shots`.
