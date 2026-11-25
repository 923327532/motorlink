using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using MotorLink.Application.Common;
using MotorLink.Domain.Common;
using MotorLink.Domain.Entities;
using MotorLink.Domain.Exceptions;
using MotorLink.Domain.Rules;

namespace MotorLink.Application.Vehicles;

public sealed record VehicleImageDto(Guid Id, string Url, int SortOrder, bool IsCover);

public sealed record VehicleRequest
{
    [Range(1, int.MaxValue)] public int VehicleTypeId { get; init; }
    [Range(1, int.MaxValue)] public int BrandId { get; init; }
    [Range(1, int.MaxValue)] public int ModelId { get; init; }
    [StringLength(80)] public string? Version { get; init; }
    public int ManufactureYear { get; init; }
    public int ModelYear { get; init; }
    [StringLength(12)] public string? Plate { get; init; }
    [StringLength(40)] public string? Color { get; init; }
    [Range(0, 3_000_000)] public int Mileage { get; init; }
    public VehicleCondition Condition { get; init; }
    [Range(0, 30)] public int PreviousOwners { get; init; }
    public FuelType FuelType { get; init; }
    public TransmissionType Transmission { get; init; }
    [Range(0, 20000)] public int? EngineDisplacementCc { get; init; }
    [Range(0, 8)] public int? Doors { get; init; }
    [Range(1, 100)] public int? Seats { get; init; }
    public TractionType? Traction { get; init; }
    [Range(0, 100_000)] public int? LoadCapacityKg { get; init; }
    public bool HasAirConditioning { get; init; }
    public bool HasGps { get; init; }
    public bool HasInsurance { get; init; }
    public bool HasValidSoat { get; init; }
    public bool HasValidTechnicalReview { get; init; }
    [StringLength(2000)] public string? Description { get; init; }
    /// <summary>Solo si el propietario legal es otra persona distinta al anunciante (dato declarado, no verificado).</summary>
    [StringLength(150)] public string? LegalOwnerName { get; init; }
    [StringLength(80)] public string? LegalOwnerRelationship { get; init; }
}

/// <summary>Vista privada para el usuario que registró el vehículo (incluye la placa completa).</summary>
public sealed record VehicleDto(
    Guid Id, Guid OwnerId, int VehicleTypeId, string VehicleTypeName, int BrandId, string BrandName, int ModelId, string ModelName,
    string? Version, int ManufactureYear, int ModelYear, string? Plate, string? Color, int Mileage, VehicleCondition Condition,
    int PreviousOwners, FuelType FuelType, TransmissionType Transmission, int? EngineDisplacementCc, int? Doors, int? Seats,
    TractionType? Traction, int? LoadCapacityKg, bool HasAirConditioning, bool HasGps, bool HasInsurance, bool HasValidSoat,
    bool HasValidTechnicalReview, string? Description, string? LegalOwnerName, string? LegalOwnerRelationship,
    bool OwnershipVerified, DateTime CreatedAt, DateTime? UpdatedAt, IReadOnlyList<VehicleImageDto> Images);

public interface IVehicleService
{
    Task<VehicleDto> CreateAsync(Guid userId, VehicleRequest r, CancellationToken ct);
    Task<PagedResult<VehicleDto>> ListMineAsync(Guid userId, int page, int pageSize, CancellationToken ct);
    Task<VehicleDto> GetMineAsync(Guid userId, Guid id, CancellationToken ct);
    Task<VehicleDto> UpdateAsync(Guid userId, Guid id, VehicleRequest r, CancellationToken ct);
    Task DeleteAsync(Guid userId, Guid id, CancellationToken ct);
}

public static partial class PlateNormalizer
{
    [GeneratedRegex("[^A-Za-z0-9]")] private static partial Regex NonAlphanumeric();
    [GeneratedRegex("^[A-Z0-9]{5,7}$")] private static partial Regex Valid();

    public static string? Normalize(string? plate)
    {
        if (string.IsNullOrWhiteSpace(plate)) return null;
        return NonAlphanumeric().Replace(plate, "").ToUpperInvariant();
    }

    public static bool IsValid(string normalized) => Valid().IsMatch(normalized);

    /// <summary>Máscara para endpoints públicos: se conservan solo los 2 primeros caracteres.</summary>
    public static string? Mask(string? normalized) =>
        string.IsNullOrEmpty(normalized) ? null : normalized[..Math.Min(2, normalized.Length)] + new string('*', Math.Max(normalized.Length - 2, 0));
}

/// <summary>Reglas técnicas por tipo de vehículo.</summary>
public static class VehicleSpecValidator
{
    private sealed record Rule(int? MinDoors, int? MaxDoors, int MinSeats, int MaxSeats, bool RequiresLoad, bool RequiresDisplacement);

    private static readonly Dictionary<string, Rule> Rules = new()
    {
        [VehicleTypeCodes.Car] = new(2, 5, 2, 9, false, true),
        [VehicleTypeCodes.Pickup] = new(2, 5, 2, 9, false, true),
        [VehicleTypeCodes.Minivan] = new(3, 5, 5, 9, false, true),
        [VehicleTypeCodes.Van] = new(2, 5, 2, 15, true, true),
        [VehicleTypeCodes.Truck] = new(2, 4, 1, 3, true, true),
        [VehicleTypeCodes.Bus] = new(1, 4, 10, 80, false, true),
        [VehicleTypeCodes.Motorcycle] = new(null, null, 1, 2, false, true),
        [VehicleTypeCodes.Mototaxi] = new(null, null, 1, 4, false, false)
    };

    public static Dictionary<string, string[]> Validate(VehicleRequest r, string typeCode, string? normalizedPlate, int currentYear)
    {
        var errors = new Dictionary<string, List<string>>();
        void Err(string field, string msg) { if (!errors.TryGetValue(field, out var l)) errors[field] = l = []; l.Add(msg); }

        if (r.ManufactureYear < 1950 || r.ManufactureYear > currentYear + 1) Err(nameof(r.ManufactureYear), $"Año de fabricación entre 1950 y {currentYear + 1}.");
        if (r.ModelYear < r.ManufactureYear || r.ModelYear > r.ManufactureYear + 1 || r.ModelYear > currentYear + 1)
            Err(nameof(r.ModelYear), "El año del modelo debe ser el de fabricación o el siguiente (sin superar el año próximo).");

        if (r.Condition == VehicleCondition.New)
        {
            if (r.Mileage > 500) Err(nameof(r.Mileage), "Un vehículo nuevo no puede superar 500 km.");
            if (r.PreviousOwners > 0) Err(nameof(r.PreviousOwners), "Un vehículo nuevo no tiene propietarios anteriores.");
        }
        else if (normalizedPlate is null)
            Err(nameof(r.Plate), "La placa es obligatoria salvo en vehículos nuevos sin matrícula.");

        if (normalizedPlate is not null && !PlateNormalizer.IsValid(normalizedPlate))
            Err(nameof(r.Plate), "Placa inválida: use 5 a 7 letras o números.");

        if (!string.IsNullOrWhiteSpace(r.LegalOwnerRelationship) && string.IsNullOrWhiteSpace(r.LegalOwnerName))
            Err(nameof(r.LegalOwnerName), "Indique el nombre del propietario legal declarado.");

        if (Rules.TryGetValue(typeCode, out var rule))
        {
            if (rule.MinDoors is null)
            {
                if (r.Doors is > 0) Err(nameof(r.Doors), "Este tipo de vehículo no tiene puertas.");
            }
            else if (r.Doors is null || r.Doors < rule.MinDoors || r.Doors > rule.MaxDoors)
                Err(nameof(r.Doors), $"Puertas entre {rule.MinDoors} y {rule.MaxDoors}.");

            if (r.Seats is null || r.Seats < rule.MinSeats || r.Seats > rule.MaxSeats)
                Err(nameof(r.Seats), $"Asientos entre {rule.MinSeats} y {rule.MaxSeats}.");
            if (rule.RequiresLoad && (r.LoadCapacityKg is null or <= 0)) Err(nameof(r.LoadCapacityKg), "La capacidad de carga es obligatoria para este tipo.");
            if (rule.RequiresDisplacement && r.FuelType != FuelType.Electric && (r.EngineDisplacementCc is null or <= 0))
                Err(nameof(r.EngineDisplacementCc), "La cilindrada es obligatoria (salvo vehículos eléctricos).");
            if (typeCode is VehicleTypeCodes.Motorcycle or VehicleTypeCodes.Mototaxi && r.Traction is not null)
                Err(nameof(r.Traction), "La tracción no aplica a este tipo de vehículo.");
        }
        return errors.ToDictionary(k => k.Key, v => v.Value.ToArray());
    }
}

public sealed class VehicleService(IApplicationDbContext db, TimeProvider clock) : IVehicleService
{
    public async Task<VehicleDto> CreateAsync(Guid userId, VehicleRequest r, CancellationToken ct)
    {
        var plate = await ValidateAsync(r, null, ct);
        var v = new Vehicle { Id = Guid.NewGuid(), OwnerId = userId };
        Apply(v, r, plate);
        db.Vehicles.Add(v);
        await db.SaveChangesAsync(ct);
        return await GetMineAsync(userId, v.Id, ct);
    }

    public async Task<PagedResult<VehicleDto>> ListMineAsync(Guid userId, int page, int pageSize, CancellationToken ct)
    {
        (page, pageSize) = Paging.Normalize(page, pageSize);
        var q = db.Vehicles.AsNoTracking().Where(v => v.OwnerId == userId);
        var total = await q.CountAsync(ct);
        var items = await Project(q.OrderByDescending(v => v.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize)).ToListAsync(ct);
        return new PagedResult<VehicleDto>(items, page, pageSize, total);
    }

    public async Task<VehicleDto> GetMineAsync(Guid userId, Guid id, CancellationToken ct) =>
        await Project(db.Vehicles.AsNoTracking().Where(v => v.Id == id && v.OwnerId == userId)).FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException("Vehículo no encontrado.");

    public async Task<VehicleDto> UpdateAsync(Guid userId, Guid id, VehicleRequest r, CancellationToken ct)
    {
        var v = await db.Vehicles.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == userId, ct) ?? throw new NotFoundException("Vehículo no encontrado.");
        var plate = await ValidateAsync(r, v.Id, ct);

        // El tipo/modelo no puede cambiar mientras exista una publicación vigente (evita incoherencias con lo ofertado).
        if ((v.ModelId != r.ModelId || v.VehicleTypeId != r.VehicleTypeId) &&
            await db.Listings.AnyAsync(l => l.VehicleId == id && l.Status != ListingStatus.Draft && l.Status != ListingStatus.Archived, ct))
            throw new ConflictException("No se puede cambiar el tipo o modelo de un vehículo con una publicación en curso.");

        Apply(v, r, plate);
        await db.SaveChangesAsync(ct);
        return await GetMineAsync(userId, id, ct);
    }

    public async Task DeleteAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var v = await db.Vehicles.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == userId, ct) ?? throw new NotFoundException("Vehículo no encontrado.");
        var listing = await db.Listings.FirstOrDefaultAsync(l => l.VehicleId == id, ct);
        if (listing is not null)
        {
            var blocking = new[] { ListingStatus.Draft, ListingStatus.Archived, ListingStatus.Rejected };
            if (!blocking.Contains(listing.Status))
                throw new ConflictException("El vehículo tiene una publicación activa. Archívela antes de eliminarlo.");
            db.Listings.Remove(listing); // borrado lógico
        }
        db.Vehicles.Remove(v);          // borrado lógico (SaveChanges lo convierte en IsDeleted)
        await db.SaveChangesAsync(ct);
    }

    private async Task<string?> ValidateAsync(VehicleRequest r, Guid? existingId, CancellationToken ct)
    {
        var type = await db.VehicleTypes.AsNoTracking().FirstOrDefaultAsync(t => t.Id == r.VehicleTypeId && t.IsActive, ct)
            ?? throw BusinessValidationException.For(nameof(r.VehicleTypeId), "Tipo de vehículo inexistente o inactivo.");
        var model = await db.Models.AsNoTracking().Include(m => m.Brand).FirstOrDefaultAsync(m => m.Id == r.ModelId, ct)
            ?? throw BusinessValidationException.For(nameof(r.ModelId), "Modelo inexistente.");
        if (!model.IsActive || !model.Brand.IsActive) throw BusinessValidationException.For(nameof(r.ModelId), "El modelo o su marca están inactivos.");
        if (model.BrandId != r.BrandId) throw BusinessValidationException.For(nameof(r.ModelId), "El modelo no pertenece a la marca indicada.");
        if (!await db.VehicleModelTypes.AnyAsync(x => x.ModelId == r.ModelId && x.VehicleTypeId == r.VehicleTypeId, ct))
            throw BusinessValidationException.For(nameof(r.ModelId), $"El modelo {model.Name} no es compatible con el tipo {type.Name}.");

        var plate = PlateNormalizer.Normalize(r.Plate);
        var errors = VehicleSpecValidator.Validate(r, type.Code, plate, clock.GetUtcNow().Year);
        if (errors.Count > 0) throw new BusinessValidationException(errors);

        if (plate is not null && await db.Vehicles.AnyAsync(v => v.Plate == plate && v.Id != existingId, ct))
            throw new ConflictException("Ya existe un vehículo activo registrado con esa placa.");
        return plate;
    }

    private static void Apply(Vehicle v, VehicleRequest r, string? plate)
    {
        v.VehicleTypeId = r.VehicleTypeId; v.ModelId = r.ModelId; v.Version = r.Version?.Trim();
        v.ManufactureYear = r.ManufactureYear; v.ModelYear = r.ModelYear; v.Plate = plate; v.Color = r.Color?.Trim();
        v.Mileage = r.Mileage; v.Condition = r.Condition; v.PreviousOwners = r.PreviousOwners; v.FuelType = r.FuelType;
        v.Transmission = r.Transmission; v.EngineDisplacementCc = r.EngineDisplacementCc; v.Doors = r.Doors; v.Seats = r.Seats;
        v.Traction = r.Traction; v.LoadCapacityKg = r.LoadCapacityKg; v.HasAirConditioning = r.HasAirConditioning;
        v.HasGps = r.HasGps; v.HasInsurance = r.HasInsurance; v.HasValidSoat = r.HasValidSoat;
        v.HasValidTechnicalReview = r.HasValidTechnicalReview; v.Description = r.Description?.Trim();
        v.LegalOwnerName = string.IsNullOrWhiteSpace(r.LegalOwnerName) ? null : r.LegalOwnerName.Trim();
        v.LegalOwnerRelationship = v.LegalOwnerName is null ? null : r.LegalOwnerRelationship?.Trim();
        v.OwnershipVerified = false; // no existe verificación real de titularidad
    }

    private static IQueryable<VehicleDto> Project(IQueryable<Vehicle> q) => q.Select(v => new VehicleDto(
        v.Id, v.OwnerId, v.VehicleTypeId, v.VehicleType.Name, v.Model.BrandId, v.Model.Brand.Name, v.ModelId, v.Model.Name,
        v.Version, v.ManufactureYear, v.ModelYear, v.Plate, v.Color, v.Mileage, v.Condition, v.PreviousOwners, v.FuelType,
        v.Transmission, v.EngineDisplacementCc, v.Doors, v.Seats, v.Traction, v.LoadCapacityKg, v.HasAirConditioning, v.HasGps,
        v.HasInsurance, v.HasValidSoat, v.HasValidTechnicalReview, v.Description, v.LegalOwnerName, v.LegalOwnerRelationship,
        v.OwnershipVerified, v.CreatedAt, v.UpdatedAt,
        v.Images.OrderBy(i => i.SortOrder).Select(i => new VehicleImageDto(i.Id, i.Url, i.SortOrder, i.IsCover)).ToList()));
}
