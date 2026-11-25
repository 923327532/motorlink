using MotorLink.Domain.Entities;

namespace MotorLink.Infrastructure.Persistence.Seed;

/// <summary>
/// Datos iniciales de ubicación (UBIGEO/INEI).
/// - Departamentos: los 25 códigos oficiales de 2 dígitos del INEI (Callao = 07).
/// - Provincias y distritos: SUBCONJUNTO DE DEMOSTRACIÓN (capitales y distritos principales).
///   Antes de producción debe cargarse el catálogo completo desde la fuente oficial del INEI/RENIEC
///   y verificar estos códigos. Ver docs/ubigeo.md.
/// </summary>
public static class GeographySeed
{
    public static readonly Department[] Departments =
    [
        D(1, "01", "Amazonas"), D(2, "02", "Áncash"), D(3, "03", "Apurímac"), D(4, "04", "Arequipa"),
        D(5, "05", "Ayacucho"), D(6, "06", "Cajamarca"), D(7, "07", "Callao"), D(8, "08", "Cusco"),
        D(9, "09", "Huancavelica"), D(10, "10", "Huánuco"), D(11, "11", "Ica"), D(12, "12", "Junín"),
        D(13, "13", "La Libertad"), D(14, "14", "Lambayeque"), D(15, "15", "Lima"), D(16, "16", "Loreto"),
        D(17, "17", "Madre de Dios"), D(18, "18", "Moquegua"), D(19, "19", "Pasco"), D(20, "20", "Piura"),
        D(21, "21", "Puno"), D(22, "22", "San Martín"), D(23, "23", "Tacna"), D(24, "24", "Tumbes"),
        D(25, "25", "Ucayali")
    ];

    // Id de provincia/distrito = valor numérico de su código UBIGEO (estable y legible).
    public static readonly Province[] Provinces =
    [
        P(701, 7, "0701", "Callao"),
        P(401, 4, "0401", "Arequipa"),
        P(801, 8, "0801", "Cusco"),
        P(1301, 13, "1301", "Trujillo"),
        P(1401, 14, "1401", "Chiclayo"),
        P(1501, 15, "1501", "Lima"),
        P(2001, 20, "2001", "Piura")
    ];

    public static readonly District[] Districts =
    [
        Di(70101, 701, "070101", "Callao"),
        Di(40101, 401, "040101", "Arequipa"),
        Di(80101, 801, "080101", "Cusco"),
        Di(130101, 1301, "130101", "Trujillo"),
        Di(140101, 1401, "140101", "Chiclayo"),
        Di(150101, 1501, "150101", "Lima"),
        Di(150103, 1501, "150103", "Ate"),
        Di(150114, 1501, "150114", "La Molina"),
        Di(150122, 1501, "150122", "Miraflores"),
        Di(150130, 1501, "150130", "San Borja"),
        Di(150131, 1501, "150131", "San Isidro"),
        Di(150132, 1501, "150132", "San Juan de Lurigancho"),
        Di(150140, 1501, "150140", "Santiago de Surco"),
        Di(150141, 1501, "150141", "Surquillo"),
        Di(200101, 2001, "200101", "Piura")
    ];

    private static Department D(int id, string code, string name) => new() { Id = id, Code = code, Name = name };
    private static Province P(int id, int dep, string code, string name) => new() { Id = id, DepartmentId = dep, Code = code, Name = name };
    private static District Di(int id, int prov, string code, string name) => new() { Id = id, ProvinceId = prov, Code = code, Name = name };
}
