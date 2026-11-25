using MotorLink.Domain.Entities;

namespace MotorLink.Infrastructure.Persistence.Seed;

/// <summary>
/// Catálogo inicial de DEMOSTRACIÓN de tipos, marcas y modelos comunes en el mercado peruano.
/// La compatibilidad modelo↔tipo es aproximada y debe completarla el administrador.
/// </summary>
public static class VehicleCatalogSeed
{
    // Ids estables entre etapas (el 6 de la etapa 1, "Station Wagon", se retiró).
    public const int Car = 1, Minivan = 2, Bus = 3, Pickup = 4, Van = 5, Motorcycle = 7, Mototaxi = 8, Truck = 9;

    public static readonly VehicleType[] VehicleTypes =
    [
        new() { Id = Car, Code = "CAR", Name = "Auto" },
        new() { Id = Minivan, Code = "MINIVAN", Name = "Miniván" },
        new() { Id = Bus, Code = "BUS", Name = "Bus" },
        new() { Id = Pickup, Code = "PICKUP", Name = "Camioneta" },
        new() { Id = Van, Code = "VAN", Name = "Furgoneta" },
        new() { Id = Motorcycle, Code = "MOTORCYCLE", Name = "Motocicleta" },
        new() { Id = Mototaxi, Code = "MOTOTAXI", Name = "Mototaxi" },
        new() { Id = Truck, Code = "TRUCK", Name = "Camión" }
    ];

    public static readonly Brand[] Brands =
    [
        new() { Id = 1, Name = "Toyota" }, new() { Id = 2, Name = "Hyundai" }, new() { Id = 3, Name = "Kia" },
        new() { Id = 4, Name = "Nissan" }, new() { Id = 5, Name = "Suzuki" }, new() { Id = 6, Name = "Chevrolet" },
        new() { Id = 7, Name = "Honda" }, new() { Id = 8, Name = "Mazda" }, new() { Id = 9, Name = "Volkswagen" },
        new() { Id = 10, Name = "Mitsubishi" }, new() { Id = 11, Name = "Bajaj" }, new() { Id = 12, Name = "Hino" },
        new() { Id = 13, Name = "Yamaha" }, new() { Id = 14, Name = "Mercedes-Benz" }
    ];

    // (id, marca, nombre, tipos compatibles)
    private static readonly (int Id, int Brand, string Name, int[] Types)[] ModelDefs =
    [
        (1, 1, "Yaris", [Car]), (2, 1, "Corolla", [Car]), (3, 1, "Hilux", [Pickup]), (4, 1, "RAV4", [Pickup]), (5, 1, "Avanza", [Minivan]),
        (6, 2, "Accent", [Car]), (7, 2, "Elantra", [Car]), (8, 2, "Tucson", [Pickup]), (9, 2, "Grand i10", [Car]),
        (10, 3, "Rio", [Car]), (11, 3, "Picanto", [Car]), (12, 3, "Sportage", [Pickup]), (13, 3, "Cerato", [Car]),
        (14, 4, "Versa", [Car]), (15, 4, "Sentra", [Car]), (16, 4, "Frontier", [Pickup]), (17, 4, "Kicks", [Pickup]),
        (18, 5, "Swift", [Car]), (19, 5, "Dzire", [Car]), (20, 5, "Vitara", [Pickup]), (21, 5, "Ertiga", [Minivan]),
        (22, 6, "Sail", [Car]), (23, 6, "Spark", [Car]), (24, 6, "N300", [Van]), (25, 6, "Onix", [Car]),
        (26, 7, "City", [Car]), (27, 7, "Civic", [Car]), (28, 7, "CR-V", [Pickup]),
        (29, 8, "Mazda 2", [Car]), (30, 8, "Mazda 3", [Car]), (31, 8, "CX-5", [Pickup]),
        (32, 9, "Gol", [Car]), (33, 9, "Polo", [Car]), (34, 9, "Tiguan", [Pickup]),
        (35, 10, "L200", [Pickup]), (36, 10, "ASX", [Pickup]),
        (37, 1, "Hiace", [Van, Minivan]), (38, 2, "H1", [Van, Minivan]), (39, 7, "CB 190R", [Motorcycle]),
        (40, 13, "FZ-25", [Motorcycle]), (41, 11, "Torito RE", [Mototaxi]), (42, 12, "Hino 300", [Truck]),
        (43, 14, "Sprinter", [Van, Minivan]), (44, 14, "O 500", [Bus]), (45, 2, "County", [Bus])
    ];

    public static readonly VehicleModel[] Models = ModelDefs.Select(m => new VehicleModel { Id = m.Id, BrandId = m.Brand, Name = m.Name }).ToArray();

    public static readonly VehicleModelType[] ModelTypes =
        ModelDefs.SelectMany(m => m.Types.Select(t => new VehicleModelType { ModelId = m.Id, VehicleTypeId = t })).ToArray();
}
