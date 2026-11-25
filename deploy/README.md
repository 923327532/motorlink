# Despliegue en producción – notas

El `docker-compose.yml` de la raíz sirve para desarrollo/demostración. Antes de producción:

1. **TLS**: colocar un proxy inverso (Nginx, Caddy, Traefik…) con certificado válido delante del servicio `web`. Debe enviar
   `X-Forwarded-For` y `X-Forwarded-Proto`. Definir `SECURE_COOKIES=true` y `ASPNETCORE_ENVIRONMENT=Production`.
2. **API**: no publicar el puerto 5080 a Internet (`API_BIND=127.0.0.1` o eliminar `ports:`). Como la API confía en `X-Forwarded-For`
   de su red privada, debe ser alcanzable solo desde `web`.
3. **SQL Server**: usar una edición con licencia (`MSSQL_PID=Standard|Enterprise|Express…`; *Developer* es solo para desarrollo/pruebas).
   Crear un login de aplicación con permisos mínimos en lugar de `sa` (la cadena de conexión del compose usa `sa` por simplicidad).
   Con `Database__MigrateOnStartup=false` en producción, aplicar migraciones con un script: `dotnet ef migrations script --idempotent`.
4. **Secretos**: usar el gestor del orquestador (Docker/Kubernetes secrets, Key Vault…) para `JWT_KEY`, contraseñas y `ADMIN_*`; rotar `JWT_KEY` invalida los access tokens vigentes.
5. **Claves de Data Protection**: están en el volumen `web_keys`, sin cifrar en reposo. Proteger el volumen o usar
   `ProtectKeysWithCertificate`/almacén externo. Hacer copia del volumen: perderlo cierra todas las sesiones web.
6. **Copias de seguridad** del volumen `mssql_data`.
