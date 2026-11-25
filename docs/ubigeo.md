# Datos de ubicación (UBIGEO)

| Tabla | Contenido sembrado | Estado |
|---|---|---|
| `Departments` | 25 departamentos con su código INEI de 2 dígitos (Callao = 07) | Completo |
| `Provinces` | 7 provincias (capitales de Lima, Callao, Arequipa, Cusco, La Libertad, Lambayeque y Piura) | **Demostración** |
| `Districts` | 15 distritos (10 de Lima, más el distrito capital de las demás provincias) | **Demostración** |

No se inventaron códigos: los incluidos corresponden a la codificación UBIGEO del INEI, pero **provincias y distritos son solo
un subconjunto de demostración y deben verificarse** contra el catálogo oficial antes de usarse en producción.

## Pendiente (antes de producción)

1. Descargar el catálogo oficial de UBIGEO del INEI/RENIEC (CSV).
2. Generar una migración de datos que cargue provincias y distritos completos (≈ 196 provincias, ≈ 1 890 distritos) y valide los códigos de 2/4/6 dígitos.
3. Reemplazar `GeographySeed.Provinces/Districts`.

Los `Id` de provincias y distritos son el valor numérico de su código UBIGEO (p. ej. distrito `150122` → Id `150122`), lo que facilita la carga posterior.
