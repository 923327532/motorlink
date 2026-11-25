using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using MotorLink.Web.Services.Api;

namespace MotorLink.Web.Areas.Dashboard.Controllers;

public sealed record WizardViewModel(
    MyVehicle? Vehicle, MyListing? Listing, IReadOnlyList<ImageInfo> Images, CurrentPlan Plan, IReadOnlyList<VehicleTypeDto> Types,
    IReadOnlyList<BrandDto> Brands, IReadOnlyList<DepartmentDto> Departments, int StartStep, string DisplayName);

/// <summary>
/// Asistente de publicación. Cada paso se guarda en la API (vehículo en los pasos 1-2, publicación en 3-4, fotos en 5), de modo que
/// el borrador persiste y se puede retomar desde "Mis vehículos"/"Mis publicaciones". Este controlador solo coordina: toda regla
/// de negocio y validación definitiva vive en el backend; los errores por campo se devuelven al navegador para mostrarlos junto al control.
/// </summary>
[Route("panel/publish")]
public sealed class PublishController(IVehiclesApi vehicles, IListingsApi listings, IBillingApi billing, ICatalogApi catalogs) : DashboardControllerBase
{
    [HttpGet("")]
    public Task<IActionResult> New(CancellationToken ct) => Load(null, ct);

    [HttpGet("{vehicleId:guid}")]
    public Task<IActionResult> Continue(Guid vehicleId, CancellationToken ct) => Load(vehicleId, ct);

    private async Task<IActionResult> Load(Guid? vehicleId, CancellationToken ct)
    {
        var planTask = billing.CurrentAsync(ct);
        var typesTask = catalogs.VehicleTypesAsync(ct);
        var brandsTask = catalogs.BrandsAsync(ct);
        var depsTask = catalogs.DepartmentsAsync(ct);
        Task<ApiResult<MyVehicle>>? vehicleTask = vehicleId is null ? null : vehicles.GetAsync(vehicleId.Value, ct);
        Task<ApiResult<PagedResult<MyListing>>>? listingsTask = vehicleId is null ? null : listings.MineAsync(null, 1, 50, ct);
        var pending = new List<Task> { planTask, typesTask, brandsTask, depsTask };
        if (vehicleTask is not null) pending.Add(vehicleTask);
        if (listingsTask is not null) pending.Add(listingsTask);
        await Task.WhenAll(pending);

        var vehicle = vehicleTask?.Result.Data();
        var listing = listingsTask?.Result.Success == true ? listingsTask.Result.Value!.Items.FirstOrDefault(l => l.VehicleId == vehicleId) : null;
        // Un vehículo recién guardado continúa en el paso 3; con borrador de publicación, en el 5 (fotos y envío).
        var step = vehicle is null ? 1 : listing is null ? 3 : 5;
        var editable = listing is null || listing.Status is "DRAFT" or "REJECTED" or "PAUSED";
        ViewData["Title"] = vehicle is null ? "Publicar vehículo" : "Editar publicación";
        ViewData["Editable"] = editable;
        return View("Index", new WizardViewModel(vehicle, listing, vehicle?.Images ?? [], planTask.Result.Data(), typesTask.Result, brandsTask.Result, depsTask.Result, step, User.Identity?.Name ?? ""));
    }

    // ---------------- guardado por pasos (JSON) ----------------
    [HttpPost("vehicle")]
    public async Task<IActionResult> SaveVehicle([FromBody] JsonElement body, Guid? id, CancellationToken ct)
    {
        var r = id is null ? await vehicles.CreateAsync(body, ct) : await vehicles.UpdateAsync(id.Value, body, ct);
        return r.Success ? Json(new { ok = true, id = r.Value!.Id }) : Fail(r.Error!);
    }

    [HttpPost("listing")]
    public async Task<IActionResult> SaveListing([FromBody] JsonElement body, Guid? id, CancellationToken ct)
    {
        var r = id is null ? await listings.CreateAsync(body, ct) : await listings.UpdateAsync(id.Value, body, ct);
        return r.Success ? Json(new { ok = true, id = r.Value!.Id, status = r.Value.Status }) : Fail(r.Error!);
    }

    public sealed record SubmitBody(Guid ListingId);

    [HttpPost("submit")]
    public async Task<IActionResult> Submit([FromBody] SubmitBody body, CancellationToken ct)
    {
        var r = await listings.ChangeStatusAsync(body.ListingId, "PENDING_REVIEW", null, ct);
        return r.Success ? Json(new { ok = true, status = r.Value!.Status }) : Fail(r.Error!);
    }

    // ---------------- fotografías ----------------
    [HttpPost("{vehicleId:guid}/photos")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> Upload(Guid vehicleId, IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return Json(new { ok = false, message = "Selecciona una imagen." }, 400);
        if (file.Length > 5 * 1024 * 1024) return Json(new { ok = false, message = "La imagen supera el máximo de 5 MB." }, 400);
        await using var stream = file.OpenReadStream();
        var r = await vehicles.UploadImageAsync(vehicleId, stream, file.FileName, file.ContentType, ct);
        return r.Success ? Json(new { ok = true, image = r.Value }) : Fail(r.Error!);
    }

    [HttpDelete("{vehicleId:guid}/photos/{imageId:guid}")]
    public async Task<IActionResult> DeletePhoto(Guid vehicleId, Guid imageId, CancellationToken ct)
    {
        var r = await vehicles.DeleteImageAsync(vehicleId, imageId, ct);
        if (!r.Success) return Fail(r.Error!);
        var list = await vehicles.ImagesAsync(vehicleId, ct);
        return Json(new { ok = true, images = list.Value });
    }

    public sealed record OrderBody(List<Guid> ImageIds);

    [HttpPut("{vehicleId:guid}/photos/order")]
    public async Task<IActionResult> Reorder(Guid vehicleId, [FromBody] OrderBody body, CancellationToken ct)
    {
        var r = await vehicles.ReorderImagesAsync(vehicleId, body.ImageIds, ct);
        return r.Success ? Json(new { ok = true, images = r.Value }) : Fail(r.Error!);
    }

    [HttpPatch("{vehicleId:guid}/photos/{imageId:guid}/cover")]
    public async Task<IActionResult> Cover(Guid vehicleId, Guid imageId, CancellationToken ct)
    {
        var r = await vehicles.SetCoverAsync(vehicleId, imageId, ct);
        return r.Success ? Json(new { ok = true, images = r.Value }) : Fail(r.Error!);
    }

    private IActionResult Fail(ApiError e) =>
        Json(new { ok = false, message = e.FieldErrors.Count == 1 ? e.FieldErrors.Values.First()[0] : e.FieldErrors.Count > 0 ? "Revisa los campos marcados." : e.Message, errors = e.FieldErrors, code = e.Code }, e.Status is >= 400 and < 600 ? e.Status : 502);

    private JsonResult Json(object value, int status) => new(value) { StatusCode = status };
}
