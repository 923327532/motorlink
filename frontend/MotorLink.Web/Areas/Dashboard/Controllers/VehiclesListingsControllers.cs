using Microsoft.AspNetCore.Mvc;
using MotorLink.Web.Extensions;
using MotorLink.Web.Services.Api;

namespace MotorLink.Web.Areas.Dashboard.Controllers;

public sealed record VehicleRow(MyVehicle Vehicle, MyListing? Listing);
public sealed record VehiclesViewModel(IReadOnlyList<VehicleRow> Rows, PagedResult<MyVehicle> Page);
public sealed record ListingsViewModel(PagedResult<MyListing> Page, string? Status, CurrentPlan Plan);

public sealed class VehiclesController(IVehiclesApi vehicles, IListingsApi listings) : DashboardControllerBase
{
    public async Task<IActionResult> Index(int page = 1, CancellationToken ct = default)
    {
        var vTask = vehicles.ListAsync(Math.Max(page, 1), 10, ct);
        var lTask = listings.MineAsync(null, 1, 50, ct);
        await Task.WhenAll(vTask, lTask);
        var v = vTask.Result.Data();
        var byVehicle = (lTask.Result.Success ? lTask.Result.Value!.Items : []).ToDictionary(l => l.VehicleId);
        ViewData["Title"] = "Mis vehículos";
        return View(new VehiclesViewModel(v.Items.Select(x => new VehicleRow(x, byVehicle.GetValueOrDefault(x.Id))).ToList(), v));
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        var v = (await vehicles.GetAsync(id, ct)).Data();
        var listing = (await listings.MineAsync(null, 1, 50, ct)) is { Success: true } r ? r.Value!.Items.FirstOrDefault(l => l.VehicleId == id) : null;
        ViewData["Title"] = $"{v.BrandName} {v.ModelName} {v.ModelYear}";
        return View(new VehicleRow(v, listing));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var r = await vehicles.DeleteAsync(id, ct);
        if (r.Success) this.Toast("success", "El vehículo fue eliminado.");
        else this.ToastError(r.Error!);
        return RedirectToAction(nameof(Index));
    }
}

public sealed class ListingsController(IListingsApi listings, IBillingApi billing) : DashboardControllerBase
{
    public async Task<IActionResult> Index(string? status, int page = 1, CancellationToken ct = default)
    {
        var lTask = listings.MineAsync(status, Math.Max(page, 1), 10, ct);
        var pTask = billing.CurrentAsync(ct);
        await Task.WhenAll(lTask, pTask);
        ViewData["Title"] = "Mis publicaciones";
        return View(new ListingsViewModel(lTask.Result.Data(), status, pTask.Result.Data()));
    }

    /// <summary>Cambia el estado respetando la máquina de estados del backend (el servidor valida cada transición y los límites del plan).</summary>
    [HttpPost]
    public async Task<IActionResult> Status(Guid id, string status, string? operationType, string? returnStatus, CancellationToken ct)
    {
        var r = await listings.ChangeStatusAsync(id, status, string.IsNullOrWhiteSpace(operationType) ? null : operationType, ct);
        if (r.Success) this.Toast("success", $"La publicación ahora está «{Ui.ListingStatus(status).ToLowerInvariant()}».");
        else this.ToastError(r.Error!);
        return RedirectToAction(nameof(Index), new { status = returnStatus });
    }

    [HttpPost]
    public async Task<IActionResult> Feature(Guid id, bool featured, CancellationToken ct)
    {
        var r = await listings.SetFeaturedAsync(id, featured, ct);
        if (r.Success) this.Toast("success", featured ? "La publicación ahora está destacada." : "Se quitó el destacado.");
        else this.ToastError(r.Error!);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var r = await listings.DeleteAsync(id, ct);
        if (r.Success) this.Toast("success", "La publicación fue eliminada.");
        else this.ToastError(r.Error!);
        return RedirectToAction(nameof(Index));
    }
}
