using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MotorLink.API.Authorization;
using MotorLink.Application.Catalogs;

namespace MotorLink.API.Controllers;

public sealed record ActiveRequest(bool IsActive);

/// <summary>Consultas públicas de catálogos (solo registros activos).</summary>
[ApiController]
[Route("api/v1/catalogs")]
[AllowAnonymous]
[Produces("application/json")]
public sealed class CatalogsController(ICatalogQueries catalogs) : ControllerBase
{
    [HttpGet("vehicle-types")]
    public async Task<IReadOnlyList<VehicleTypeDto>> VehicleTypes(CancellationToken ct) => await catalogs.VehicleTypesAsync(ct);

    [HttpGet("brands")]
    public async Task<IReadOnlyList<BrandDto>> Brands(CancellationToken ct) => await catalogs.BrandsAsync(ct);

    /// <summary>Modelos, opcionalmente filtrados por marca y/o tipo de vehículo compatible.</summary>
    [HttpGet("models")]
    public async Task<IReadOnlyList<ModelDto>> Models([FromQuery] int? brandId, [FromQuery] int? vehicleTypeId, CancellationToken ct) =>
        await catalogs.ModelsAsync(brandId, vehicleTypeId, ct);

    [HttpGet("departments")]
    public async Task<IReadOnlyList<DepartmentDto>> Departments(CancellationToken ct) => await catalogs.DepartmentsAsync(ct);

    [HttpGet("provinces")]
    public async Task<IReadOnlyList<ProvinceDto>> Provinces([FromQuery] int? departmentId, CancellationToken ct) =>
        await catalogs.ProvincesAsync(departmentId, ct);

    [HttpGet("districts")]
    public async Task<IReadOnlyList<DistrictDto>> Districts([FromQuery] int? provinceId, CancellationToken ct) =>
        await catalogs.DistrictsAsync(provinceId, ct);
}

/// <summary>CRUD administrativo de catálogos. Eliminar solo si no hay registros asociados; en otro caso, desactivar (PATCH .../active).</summary>
[ApiController]
[Route("api/v1/admin/catalogs")]
[Authorize(Policy = Policies.AdminOnly)]
[Produces("application/json")]
public sealed class AdminCatalogsController(ICatalogAdminService svc) : ControllerBase
{
    // ---- Tipos de vehículo ----
    [HttpGet("vehicle-types")] public async Task<IReadOnlyList<VehicleTypeDto>> ListTypes(CancellationToken ct) => await svc.ListVehicleTypesAsync(ct);
    [HttpPost("vehicle-types")] public async Task<IActionResult> CreateType(VehicleTypeRequest r, CancellationToken ct) { var d = await svc.UpsertVehicleTypeAsync(null, r, ct); return Created($"/api/v1/admin/catalogs/vehicle-types/{d.Id}", d); }
    [HttpPut("vehicle-types/{id:int}")] public async Task<VehicleTypeDto> UpdateType(int id, VehicleTypeRequest r, CancellationToken ct) => await svc.UpsertVehicleTypeAsync(id, r, ct);
    [HttpPatch("vehicle-types/{id:int}/active")] public async Task<IActionResult> ActivateType(int id, ActiveRequest r, CancellationToken ct) { await svc.SetVehicleTypeActiveAsync(id, r.IsActive, ct); return NoContent(); }
    [HttpDelete("vehicle-types/{id:int}")] public async Task<IActionResult> DeleteType(int id, CancellationToken ct) { await svc.DeleteVehicleTypeAsync(id, ct); return NoContent(); }

    // ---- Marcas ----
    [HttpGet("brands")] public async Task<IReadOnlyList<BrandDto>> ListBrands(CancellationToken ct) => await svc.ListBrandsAsync(ct);
    [HttpPost("brands")] public async Task<IActionResult> CreateBrand(BrandRequest r, CancellationToken ct) { var d = await svc.UpsertBrandAsync(null, r, ct); return Created($"/api/v1/admin/catalogs/brands/{d.Id}", d); }
    [HttpPut("brands/{id:int}")] public async Task<BrandDto> UpdateBrand(int id, BrandRequest r, CancellationToken ct) => await svc.UpsertBrandAsync(id, r, ct);
    [HttpPatch("brands/{id:int}/active")] public async Task<IActionResult> ActivateBrand(int id, ActiveRequest r, CancellationToken ct) { await svc.SetBrandActiveAsync(id, r.IsActive, ct); return NoContent(); }
    [HttpDelete("brands/{id:int}")] public async Task<IActionResult> DeleteBrand(int id, CancellationToken ct) { await svc.DeleteBrandAsync(id, ct); return NoContent(); }

    // ---- Modelos ----
    [HttpGet("models")] public async Task<IReadOnlyList<ModelDto>> ListModels([FromQuery] int? brandId, CancellationToken ct) => await svc.ListModelsAsync(brandId, ct);
    [HttpPost("models")] public async Task<IActionResult> CreateModel(ModelRequest r, CancellationToken ct) { var d = await svc.UpsertModelAsync(null, r, ct); return Created($"/api/v1/admin/catalogs/models/{d.Id}", d); }
    [HttpPut("models/{id:int}")] public async Task<ModelDto> UpdateModel(int id, ModelRequest r, CancellationToken ct) => await svc.UpsertModelAsync(id, r, ct);
    [HttpPatch("models/{id:int}/active")] public async Task<IActionResult> ActivateModel(int id, ActiveRequest r, CancellationToken ct) { await svc.SetModelActiveAsync(id, r.IsActive, ct); return NoContent(); }
    [HttpDelete("models/{id:int}")] public async Task<IActionResult> DeleteModel(int id, CancellationToken ct) { await svc.DeleteModelAsync(id, ct); return NoContent(); }

    // ---- Departamentos ----
    [HttpGet("departments")] public async Task<IReadOnlyList<DepartmentDto>> ListDepartments(CancellationToken ct) => await svc.ListDepartmentsAsync(ct);
    [HttpPost("departments")] public async Task<IActionResult> CreateDepartment(DepartmentRequest r, CancellationToken ct) { var d = await svc.UpsertDepartmentAsync(null, r, ct); return Created($"/api/v1/admin/catalogs/departments/{d.Id}", d); }
    [HttpPut("departments/{id:int}")] public async Task<DepartmentDto> UpdateDepartment(int id, DepartmentRequest r, CancellationToken ct) => await svc.UpsertDepartmentAsync(id, r, ct);
    [HttpPatch("departments/{id:int}/active")] public async Task<IActionResult> ActivateDepartment(int id, ActiveRequest r, CancellationToken ct) { await svc.SetDepartmentActiveAsync(id, r.IsActive, ct); return NoContent(); }
    [HttpDelete("departments/{id:int}")] public async Task<IActionResult> DeleteDepartment(int id, CancellationToken ct) { await svc.DeleteDepartmentAsync(id, ct); return NoContent(); }

    // ---- Provincias ----
    [HttpGet("provinces")] public async Task<IReadOnlyList<ProvinceDto>> ListProvinces([FromQuery] int? departmentId, CancellationToken ct) => await svc.ListProvincesAsync(departmentId, ct);
    [HttpPost("provinces")] public async Task<IActionResult> CreateProvince(ProvinceRequest r, CancellationToken ct) { var d = await svc.UpsertProvinceAsync(null, r, ct); return Created($"/api/v1/admin/catalogs/provinces/{d.Id}", d); }
    [HttpPut("provinces/{id:int}")] public async Task<ProvinceDto> UpdateProvince(int id, ProvinceRequest r, CancellationToken ct) => await svc.UpsertProvinceAsync(id, r, ct);
    [HttpPatch("provinces/{id:int}/active")] public async Task<IActionResult> ActivateProvince(int id, ActiveRequest r, CancellationToken ct) { await svc.SetProvinceActiveAsync(id, r.IsActive, ct); return NoContent(); }
    [HttpDelete("provinces/{id:int}")] public async Task<IActionResult> DeleteProvince(int id, CancellationToken ct) { await svc.DeleteProvinceAsync(id, ct); return NoContent(); }

    // ---- Distritos ----
    [HttpGet("districts")] public async Task<IReadOnlyList<DistrictDto>> ListDistricts([FromQuery] int? provinceId, CancellationToken ct) => await svc.ListDistrictsAsync(provinceId, ct);
    [HttpPost("districts")] public async Task<IActionResult> CreateDistrict(DistrictRequest r, CancellationToken ct) { var d = await svc.UpsertDistrictAsync(null, r, ct); return Created($"/api/v1/admin/catalogs/districts/{d.Id}", d); }
    [HttpPut("districts/{id:int}")] public async Task<DistrictDto> UpdateDistrict(int id, DistrictRequest r, CancellationToken ct) => await svc.UpsertDistrictAsync(id, r, ct);
    [HttpPatch("districts/{id:int}/active")] public async Task<IActionResult> ActivateDistrict(int id, ActiveRequest r, CancellationToken ct) { await svc.SetDistrictActiveAsync(id, r.IsActive, ct); return NoContent(); }
    [HttpDelete("districts/{id:int}")] public async Task<IActionResult> DeleteDistrict(int id, CancellationToken ct) { await svc.DeleteDistrictAsync(id, ct); return NoContent(); }
}
