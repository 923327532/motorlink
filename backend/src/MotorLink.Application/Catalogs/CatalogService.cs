using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using MotorLink.Application.Common;
using MotorLink.Domain.Entities;
using MotorLink.Domain.Exceptions;

namespace MotorLink.Application.Catalogs;

public sealed record VehicleTypeDto(int Id, string Code, string Name, bool IsActive);
public sealed record BrandDto(int Id, string Name, bool IsActive);
public sealed record ModelDto(int Id, int BrandId, string Name, IReadOnlyList<int> VehicleTypeIds, bool IsActive);
public sealed record DepartmentDto(int Id, string Code, string Name, bool IsActive);
public sealed record ProvinceDto(int Id, int DepartmentId, string Code, string Name, bool IsActive);
public sealed record DistrictDto(int Id, int ProvinceId, string Code, string Name, bool IsActive);

public sealed record VehicleTypeRequest(
    [Required, RegularExpression("^[A-Z][A-Z0-9_]{1,29}$", ErrorMessage = "Código en MAYÚSCULAS (A-Z, 0-9, _).")] string Code,
    [Required, StringLength(60)] string Name);

public sealed record BrandRequest([Required, StringLength(60)] string Name);

public sealed record ModelRequest(
    [Range(1, int.MaxValue)] int BrandId,
    [Required, StringLength(80)] string Name,
    [Required, MinLength(1)] int[] VehicleTypeIds);

public sealed record DepartmentRequest(
    [Required, RegularExpression(@"^\d{2}$", ErrorMessage = "Código UBIGEO de 2 dígitos.")] string Code,
    [Required, StringLength(100)] string Name);

public sealed record ProvinceRequest(
    [Range(1, int.MaxValue)] int DepartmentId,
    [Required, RegularExpression(@"^\d{4}$", ErrorMessage = "Código UBIGEO de 4 dígitos.")] string Code,
    [Required, StringLength(100)] string Name);

public sealed record DistrictRequest(
    [Range(1, int.MaxValue)] int ProvinceId,
    [Required, RegularExpression(@"^\d{6}$", ErrorMessage = "Código UBIGEO de 6 dígitos.")] string Code,
    [Required, StringLength(100)] string Name);

/// <summary>Consultas públicas: solo registros activos.</summary>
public interface ICatalogQueries
{
    Task<IReadOnlyList<VehicleTypeDto>> VehicleTypesAsync(CancellationToken ct);
    Task<IReadOnlyList<BrandDto>> BrandsAsync(CancellationToken ct);
    Task<IReadOnlyList<ModelDto>> ModelsAsync(int? brandId, int? vehicleTypeId, CancellationToken ct);
    Task<IReadOnlyList<DepartmentDto>> DepartmentsAsync(CancellationToken ct);
    Task<IReadOnlyList<ProvinceDto>> ProvincesAsync(int? departmentId, CancellationToken ct);
    Task<IReadOnlyList<DistrictDto>> DistrictsAsync(int? provinceId, CancellationToken ct);
}

public interface ICatalogAdminService
{
    Task<IReadOnlyList<VehicleTypeDto>> ListVehicleTypesAsync(CancellationToken ct);
    Task<VehicleTypeDto> UpsertVehicleTypeAsync(int? id, VehicleTypeRequest r, CancellationToken ct);
    Task SetVehicleTypeActiveAsync(int id, bool active, CancellationToken ct);
    Task DeleteVehicleTypeAsync(int id, CancellationToken ct);

    Task<IReadOnlyList<BrandDto>> ListBrandsAsync(CancellationToken ct);
    Task<BrandDto> UpsertBrandAsync(int? id, BrandRequest r, CancellationToken ct);
    Task SetBrandActiveAsync(int id, bool active, CancellationToken ct);
    Task DeleteBrandAsync(int id, CancellationToken ct);

    Task<IReadOnlyList<ModelDto>> ListModelsAsync(int? brandId, CancellationToken ct);
    Task<ModelDto> UpsertModelAsync(int? id, ModelRequest r, CancellationToken ct);
    Task SetModelActiveAsync(int id, bool active, CancellationToken ct);
    Task DeleteModelAsync(int id, CancellationToken ct);

    Task<IReadOnlyList<DepartmentDto>> ListDepartmentsAsync(CancellationToken ct);
    Task<DepartmentDto> UpsertDepartmentAsync(int? id, DepartmentRequest r, CancellationToken ct);
    Task SetDepartmentActiveAsync(int id, bool active, CancellationToken ct);
    Task DeleteDepartmentAsync(int id, CancellationToken ct);

    Task<IReadOnlyList<ProvinceDto>> ListProvincesAsync(int? departmentId, CancellationToken ct);
    Task<ProvinceDto> UpsertProvinceAsync(int? id, ProvinceRequest r, CancellationToken ct);
    Task SetProvinceActiveAsync(int id, bool active, CancellationToken ct);
    Task DeleteProvinceAsync(int id, CancellationToken ct);

    Task<IReadOnlyList<DistrictDto>> ListDistrictsAsync(int? provinceId, CancellationToken ct);
    Task<DistrictDto> UpsertDistrictAsync(int? id, DistrictRequest r, CancellationToken ct);
    Task SetDistrictActiveAsync(int id, bool active, CancellationToken ct);
    Task DeleteDistrictAsync(int id, CancellationToken ct);
}

public sealed class CatalogQueries(IApplicationDbContext db) : ICatalogQueries
{
    public async Task<IReadOnlyList<VehicleTypeDto>> VehicleTypesAsync(CancellationToken ct) =>
        await db.VehicleTypes.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name)
            .Select(x => new VehicleTypeDto(x.Id, x.Code, x.Name, x.IsActive)).ToListAsync(ct);

    public async Task<IReadOnlyList<BrandDto>> BrandsAsync(CancellationToken ct) =>
        await db.Brands.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name)
            .Select(x => new BrandDto(x.Id, x.Name, x.IsActive)).ToListAsync(ct);

    public async Task<IReadOnlyList<ModelDto>> ModelsAsync(int? brandId, int? vehicleTypeId, CancellationToken ct) =>
        await db.Models.AsNoTracking().Where(x => x.IsActive && x.Brand.IsActive)
            .Where(x => brandId == null || x.BrandId == brandId)
            .Where(x => vehicleTypeId == null || x.CompatibleTypes.Any(t => t.VehicleTypeId == vehicleTypeId))
            .OrderBy(x => x.Name)
            .Select(x => new ModelDto(x.Id, x.BrandId, x.Name, x.CompatibleTypes.Select(t => t.VehicleTypeId).ToList(), x.IsActive))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<DepartmentDto>> DepartmentsAsync(CancellationToken ct) =>
        await db.Departments.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name)
            .Select(x => new DepartmentDto(x.Id, x.Code, x.Name, x.IsActive)).ToListAsync(ct);

    public async Task<IReadOnlyList<ProvinceDto>> ProvincesAsync(int? departmentId, CancellationToken ct) =>
        await db.Provinces.AsNoTracking().Where(x => x.IsActive && (departmentId == null || x.DepartmentId == departmentId))
            .OrderBy(x => x.Name).Select(x => new ProvinceDto(x.Id, x.DepartmentId, x.Code, x.Name, x.IsActive)).ToListAsync(ct);

    public async Task<IReadOnlyList<DistrictDto>> DistrictsAsync(int? provinceId, CancellationToken ct) =>
        await db.Districts.AsNoTracking().Where(x => x.IsActive && (provinceId == null || x.ProvinceId == provinceId))
            .OrderBy(x => x.Name).Select(x => new DistrictDto(x.Id, x.ProvinceId, x.Code, x.Name, x.IsActive)).ToListAsync(ct);
}

public sealed class CatalogAdminService(IApplicationDbContext db, IAuditService audit) : ICatalogAdminService
{
    private const string InUse = "No se puede eliminar: tiene registros asociados. Puede desactivarlo en su lugar.";

    // ---------- Tipos de vehículo ----------
    public async Task<IReadOnlyList<VehicleTypeDto>> ListVehicleTypesAsync(CancellationToken ct) =>
        await db.VehicleTypes.AsNoTracking().OrderBy(x => x.Name).Select(x => new VehicleTypeDto(x.Id, x.Code, x.Name, x.IsActive)).ToListAsync(ct);

    public async Task<VehicleTypeDto> UpsertVehicleTypeAsync(int? id, VehicleTypeRequest r, CancellationToken ct)
    {
        var e = id is null ? new VehicleType { Id = await NextId(db.VehicleTypes.Select(x => x.Id), ct) } : await Find(db.VehicleTypes, id.Value, ct, "Tipo de vehículo");
        if (await db.VehicleTypes.AnyAsync(x => x.Code == r.Code && x.Id != e.Id, ct)) throw new ConflictException($"Ya existe un tipo con código {r.Code}.");
        e.Code = r.Code; e.Name = r.Name.Trim();
        if (id is null) db.VehicleTypes.Add(e);
        audit.Log(id is null ? "catalog.vehicle-type.create" : "catalog.vehicle-type.update", nameof(VehicleType), e.Id.ToString());
        await db.SaveChangesAsync(ct);
        return new(e.Id, e.Code, e.Name, e.IsActive);
    }

    public async Task SetVehicleTypeActiveAsync(int id, bool active, CancellationToken ct)
    {
        var e = await Find(db.VehicleTypes, id, ct, "Tipo de vehículo"); e.IsActive = active;
        audit.Log(active ? "catalog.vehicle-type.activate" : "catalog.vehicle-type.deactivate", nameof(VehicleType), id.ToString());
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteVehicleTypeAsync(int id, CancellationToken ct)
    {
        var e = await Find(db.VehicleTypes, id, ct, "Tipo de vehículo");
        if (await db.Vehicles.IgnoreQueryFilters().AnyAsync(x => x.VehicleTypeId == id, ct) || await db.VehicleModelTypes.AnyAsync(x => x.VehicleTypeId == id, ct))
            throw new ConflictException(InUse);
        db.VehicleTypes.Remove(e);
        audit.Log("catalog.vehicle-type.delete", nameof(VehicleType), id.ToString());
        await db.SaveChangesAsync(ct);
    }

    // ---------- Marcas ----------
    public async Task<IReadOnlyList<BrandDto>> ListBrandsAsync(CancellationToken ct) =>
        await db.Brands.AsNoTracking().OrderBy(x => x.Name).Select(x => new BrandDto(x.Id, x.Name, x.IsActive)).ToListAsync(ct);

    public async Task<BrandDto> UpsertBrandAsync(int? id, BrandRequest r, CancellationToken ct)
    {
        var e = id is null ? new Brand { Id = await NextId(db.Brands.Select(x => x.Id), ct) } : await Find(db.Brands, id.Value, ct, "Marca");
        var name = r.Name.Trim();
        if (await db.Brands.AnyAsync(x => x.Name == name && x.Id != e.Id, ct)) throw new ConflictException($"Ya existe la marca {name}.");
        e.Name = name;
        if (id is null) db.Brands.Add(e);
        audit.Log(id is null ? "catalog.brand.create" : "catalog.brand.update", nameof(Brand), e.Id.ToString());
        await db.SaveChangesAsync(ct);
        return new(e.Id, e.Name, e.IsActive);
    }

    public async Task SetBrandActiveAsync(int id, bool active, CancellationToken ct)
    {
        var e = await Find(db.Brands, id, ct, "Marca"); e.IsActive = active;
        audit.Log(active ? "catalog.brand.activate" : "catalog.brand.deactivate", nameof(Brand), id.ToString());
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteBrandAsync(int id, CancellationToken ct)
    {
        var e = await Find(db.Brands, id, ct, "Marca");
        if (await db.Models.AnyAsync(x => x.BrandId == id, ct)) throw new ConflictException(InUse);
        db.Brands.Remove(e);
        audit.Log("catalog.brand.delete", nameof(Brand), id.ToString());
        await db.SaveChangesAsync(ct);
    }

    // ---------- Modelos ----------
    public async Task<IReadOnlyList<ModelDto>> ListModelsAsync(int? brandId, CancellationToken ct) =>
        await db.Models.AsNoTracking().Where(x => brandId == null || x.BrandId == brandId).OrderBy(x => x.Name)
            .Select(x => new ModelDto(x.Id, x.BrandId, x.Name, x.CompatibleTypes.Select(t => t.VehicleTypeId).ToList(), x.IsActive)).ToListAsync(ct);

    public async Task<ModelDto> UpsertModelAsync(int? id, ModelRequest r, CancellationToken ct)
    {
        if (!await db.Brands.AnyAsync(x => x.Id == r.BrandId, ct)) throw BusinessValidationException.For(nameof(r.BrandId), "La marca no existe.");
        var typeIds = r.VehicleTypeIds.Distinct().ToArray();
        if (await db.VehicleTypes.CountAsync(x => typeIds.Contains(x.Id), ct) != typeIds.Length)
            throw BusinessValidationException.For(nameof(r.VehicleTypeIds), "Algún tipo de vehículo no existe.");

        var e = id is null
            ? new VehicleModel { Id = await NextId(db.Models.Select(x => x.Id), ct) }
            : await db.Models.Include(x => x.CompatibleTypes).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Modelo no encontrado.");
        var name = r.Name.Trim();
        if (await db.Models.AnyAsync(x => x.BrandId == r.BrandId && x.Name == name && x.Id != e.Id, ct))
            throw new ConflictException($"La marca ya tiene un modelo {name}.");
        e.BrandId = r.BrandId; e.Name = name;

        // Cambiar tipos compatibles no puede dejar vehículos registrados con un tipo no permitido.
        if (id is not null)
        {
            var removed = e.CompatibleTypes.Where(t => !typeIds.Contains(t.VehicleTypeId)).Select(t => t.VehicleTypeId).ToList();
            if (removed.Count > 0 && await db.Vehicles.IgnoreQueryFilters().AnyAsync(v => v.ModelId == e.Id && removed.Contains(v.VehicleTypeId), ct))
                throw new ConflictException("Hay vehículos registrados con un tipo que intenta quitar del modelo.");
            foreach (var t in e.CompatibleTypes.Where(t => removed.Contains(t.VehicleTypeId)).ToList()) e.CompatibleTypes.Remove(t);
        }
        foreach (var tid in typeIds.Where(t => e.CompatibleTypes.All(x => x.VehicleTypeId != t)))
            e.CompatibleTypes.Add(new VehicleModelType { ModelId = e.Id, VehicleTypeId = tid });

        if (id is null) db.Models.Add(e);
        audit.Log(id is null ? "catalog.model.create" : "catalog.model.update", nameof(VehicleModel), e.Id.ToString());
        await db.SaveChangesAsync(ct);
        return new(e.Id, e.BrandId, e.Name, e.CompatibleTypes.Select(t => t.VehicleTypeId).ToList(), e.IsActive);
    }

    public async Task SetModelActiveAsync(int id, bool active, CancellationToken ct)
    {
        var e = await Find(db.Models, id, ct, "Modelo"); e.IsActive = active;
        audit.Log(active ? "catalog.model.activate" : "catalog.model.deactivate", nameof(VehicleModel), id.ToString());
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteModelAsync(int id, CancellationToken ct)
    {
        var e = await db.Models.Include(x => x.CompatibleTypes).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Modelo no encontrado.");
        if (await db.Vehicles.IgnoreQueryFilters().AnyAsync(x => x.ModelId == id, ct)) throw new ConflictException(InUse);
        db.Models.Remove(e);
        audit.Log("catalog.model.delete", nameof(VehicleModel), id.ToString());
        await db.SaveChangesAsync(ct);
    }

    // ---------- Departamentos ----------
    public async Task<IReadOnlyList<DepartmentDto>> ListDepartmentsAsync(CancellationToken ct) =>
        await db.Departments.AsNoTracking().OrderBy(x => x.Name).Select(x => new DepartmentDto(x.Id, x.Code, x.Name, x.IsActive)).ToListAsync(ct);

    public async Task<DepartmentDto> UpsertDepartmentAsync(int? id, DepartmentRequest r, CancellationToken ct)
    {
        var e = id is null ? new Department { Id = int.Parse(r.Code) } : await Find(db.Departments, id.Value, ct, "Departamento");
        if (id is not null && e.Code != r.Code) throw BusinessValidationException.For(nameof(r.Code), "El código UBIGEO no puede modificarse.");
        if (id is null && await db.Departments.AnyAsync(x => x.Code == r.Code, ct)) throw new ConflictException($"Ya existe el departamento {r.Code}.");
        e.Code = r.Code; e.Name = r.Name.Trim();
        if (id is null) db.Departments.Add(e);
        audit.Log(id is null ? "catalog.department.create" : "catalog.department.update", nameof(Department), e.Id.ToString());
        await db.SaveChangesAsync(ct);
        return new(e.Id, e.Code, e.Name, e.IsActive);
    }

    public async Task SetDepartmentActiveAsync(int id, bool active, CancellationToken ct)
    {
        var e = await Find(db.Departments, id, ct, "Departamento"); e.IsActive = active;
        audit.Log(active ? "catalog.department.activate" : "catalog.department.deactivate", nameof(Department), id.ToString());
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteDepartmentAsync(int id, CancellationToken ct)
    {
        var e = await Find(db.Departments, id, ct, "Departamento");
        if (await db.Provinces.AnyAsync(x => x.DepartmentId == id, ct) || await db.Listings.IgnoreQueryFilters().AnyAsync(x => x.DepartmentId == id, ct))
            throw new ConflictException(InUse);
        db.Departments.Remove(e);
        audit.Log("catalog.department.delete", nameof(Department), id.ToString());
        await db.SaveChangesAsync(ct);
    }

    // ---------- Provincias ----------
    public async Task<IReadOnlyList<ProvinceDto>> ListProvincesAsync(int? departmentId, CancellationToken ct) =>
        await db.Provinces.AsNoTracking().Where(x => departmentId == null || x.DepartmentId == departmentId).OrderBy(x => x.Name)
            .Select(x => new ProvinceDto(x.Id, x.DepartmentId, x.Code, x.Name, x.IsActive)).ToListAsync(ct);

    public async Task<ProvinceDto> UpsertProvinceAsync(int? id, ProvinceRequest r, CancellationToken ct)
    {
        var dep = await db.Departments.FirstOrDefaultAsync(x => x.Id == r.DepartmentId, ct) ?? throw BusinessValidationException.For(nameof(r.DepartmentId), "El departamento no existe.");
        if (!r.Code.StartsWith(dep.Code, StringComparison.Ordinal)) throw BusinessValidationException.For(nameof(r.Code), $"El código debe iniciar con el del departamento ({dep.Code}).");
        var e = id is null ? new Province { Id = int.Parse(r.Code) } : await Find(db.Provinces, id.Value, ct, "Provincia");
        if (id is not null && e.Code != r.Code) throw BusinessValidationException.For(nameof(r.Code), "El código UBIGEO no puede modificarse.");
        if (id is null && await db.Provinces.AnyAsync(x => x.Code == r.Code, ct)) throw new ConflictException($"Ya existe la provincia {r.Code}.");
        e.DepartmentId = r.DepartmentId; e.Code = r.Code; e.Name = r.Name.Trim();
        if (id is null) db.Provinces.Add(e);
        audit.Log(id is null ? "catalog.province.create" : "catalog.province.update", nameof(Province), e.Id.ToString());
        await db.SaveChangesAsync(ct);
        return new(e.Id, e.DepartmentId, e.Code, e.Name, e.IsActive);
    }

    public async Task SetProvinceActiveAsync(int id, bool active, CancellationToken ct)
    {
        var e = await Find(db.Provinces, id, ct, "Provincia"); e.IsActive = active;
        audit.Log(active ? "catalog.province.activate" : "catalog.province.deactivate", nameof(Province), id.ToString());
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteProvinceAsync(int id, CancellationToken ct)
    {
        var e = await Find(db.Provinces, id, ct, "Provincia");
        if (await db.Districts.AnyAsync(x => x.ProvinceId == id, ct) || await db.Listings.IgnoreQueryFilters().AnyAsync(x => x.ProvinceId == id, ct))
            throw new ConflictException(InUse);
        db.Provinces.Remove(e);
        audit.Log("catalog.province.delete", nameof(Province), id.ToString());
        await db.SaveChangesAsync(ct);
    }

    // ---------- Distritos ----------
    public async Task<IReadOnlyList<DistrictDto>> ListDistrictsAsync(int? provinceId, CancellationToken ct) =>
        await db.Districts.AsNoTracking().Where(x => provinceId == null || x.ProvinceId == provinceId).OrderBy(x => x.Name)
            .Select(x => new DistrictDto(x.Id, x.ProvinceId, x.Code, x.Name, x.IsActive)).ToListAsync(ct);

    public async Task<DistrictDto> UpsertDistrictAsync(int? id, DistrictRequest r, CancellationToken ct)
    {
        var prov = await db.Provinces.FirstOrDefaultAsync(x => x.Id == r.ProvinceId, ct) ?? throw BusinessValidationException.For(nameof(r.ProvinceId), "La provincia no existe.");
        if (!r.Code.StartsWith(prov.Code, StringComparison.Ordinal)) throw BusinessValidationException.For(nameof(r.Code), $"El código debe iniciar con el de la provincia ({prov.Code}).");
        var e = id is null ? new District { Id = int.Parse(r.Code) } : await Find(db.Districts, id.Value, ct, "Distrito");
        if (id is not null && e.Code != r.Code) throw BusinessValidationException.For(nameof(r.Code), "El código UBIGEO no puede modificarse.");
        if (id is null && await db.Districts.AnyAsync(x => x.Code == r.Code, ct)) throw new ConflictException($"Ya existe el distrito {r.Code}.");
        e.ProvinceId = r.ProvinceId; e.Code = r.Code; e.Name = r.Name.Trim();
        if (id is null) db.Districts.Add(e);
        audit.Log(id is null ? "catalog.district.create" : "catalog.district.update", nameof(District), e.Id.ToString());
        await db.SaveChangesAsync(ct);
        return new(e.Id, e.ProvinceId, e.Code, e.Name, e.IsActive);
    }

    public async Task SetDistrictActiveAsync(int id, bool active, CancellationToken ct)
    {
        var e = await Find(db.Districts, id, ct, "Distrito"); e.IsActive = active;
        audit.Log(active ? "catalog.district.activate" : "catalog.district.deactivate", nameof(District), id.ToString());
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteDistrictAsync(int id, CancellationToken ct)
    {
        var e = await Find(db.Districts, id, ct, "Distrito");
        if (await db.Listings.IgnoreQueryFilters().AnyAsync(x => x.DistrictId == id, ct)
            || await db.UserProfiles.AnyAsync(x => x.DistrictId == id, ct)
            || await db.BusinessProfiles.IgnoreQueryFilters().AnyAsync(x => x.DistrictId == id, ct))
            throw new ConflictException(InUse);
        db.Districts.Remove(e);
        audit.Log("catalog.district.delete", nameof(District), id.ToString());
        await db.SaveChangesAsync(ct);
    }

    // ---------- helpers ----------
    private static async Task<T> Find<T>(DbSet<T> set, int id, CancellationToken ct, string label) where T : class =>
        await set.FindAsync([id], ct) ?? throw new NotFoundException($"{label} no encontrado.");

    /// <summary>Los catálogos con semilla usan claves manuales; el siguiente Id es MAX+1 (la PK evita colisiones).</summary>
    private static async Task<int> NextId(IQueryable<int> ids, CancellationToken ct) =>
        (await ids.OrderByDescending(x => x).Select(x => (int?)x).FirstOrDefaultAsync(ct) ?? 0) + 1;
}
