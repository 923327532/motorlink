using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using MotorLink.Web.Extensions;
using MotorLink.Web.Services.Api;

namespace MotorLink.Web.Areas.Admin.Controllers;

/// <summary>Base de la administración: área Admin protegida por el rol ADMIN (la API vuelve a validarlo en cada llamada).</summary>
[Area("Admin"), Authorize(Roles = "ADMIN")]
public abstract class AdminControllerBase : Controller
{
    protected IActionResult Back(string action, object? route = null) => RedirectToAction(action, route);

    protected void Report<T>(ApiResult<T> r, string success)
    {
        if (r.Success) this.Toast("success", success); else this.ToastError(r.Error!);
    }
}

// =========================================================================== Dashboard
public sealed record AdminHomeViewModel(AdminStats Stats, int PendingModeration);

public sealed class HomeController(IAdminApi admin) : AdminControllerBase
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var statsTask = admin.StatsAsync(ct);
        var pendingTask = admin.ListingsAsync("PENDING_REVIEW", 1, 1, ct);
        await Task.WhenAll(statsTask, pendingTask);
        ViewData["Title"] = "Dashboard";
        return View(new AdminHomeViewModel(statsTask.Result.Data(), pendingTask.Result.Success ? pendingTask.Result.Value!.TotalCount : 0));
    }
}

// =========================================================================== Personas
public sealed record UsersViewModel(PagedResult<AdminUser> Page, string? Search, bool? Active);

public sealed class UsersController(IAdminApi admin) : AdminControllerBase
{
    public async Task<IActionResult> Index(string? search, bool? active, int page = 1, CancellationToken ct = default)
    {
        ViewData["Title"] = "Usuarios";
        return View(new UsersViewModel((await admin.UsersAsync(search, active, Math.Max(page, 1), 15, ct)).Data(), search, active));
    }

    [HttpPost]
    public async Task<IActionResult> SetStatus(Guid id, bool active, string? search, CancellationToken ct)
    {
        Report(await admin.SetUserActiveAsync(id, active, ct), active ? "La cuenta fue reactivada." : "La cuenta fue bloqueada y sus sesiones cerradas.");
        return RedirectToAction(nameof(Index), new { search });
    }
}

public sealed class BusinessesController(IAdminApi admin) : AdminControllerBase
{
    public async Task<IActionResult> Index(string? search, int page = 1, CancellationToken ct = default)
    {
        ViewData["Title"] = "Empresas";
        ViewData["Search"] = search;
        return View((await admin.BusinessesAsync(search, Math.Max(page, 1), 15, ct)).Data());
    }
}

// =========================================================================== Marketplace
public sealed class VehiclesController(IAdminApi admin) : AdminControllerBase
{
    public async Task<IActionResult> Index(string? search, int page = 1, CancellationToken ct = default)
    {
        ViewData["Title"] = "Vehículos";
        ViewData["Search"] = search;
        return View((await admin.VehiclesAsync(search, Math.Max(page, 1), 15, ct)).Data());
    }
}

public sealed record ModerationViewModel(PagedResult<ModerationItem> Page, string Status);

public sealed class ListingsController(IAdminApi admin) : AdminControllerBase
{
    public async Task<IActionResult> Index(string? status, int page = 1, CancellationToken ct = default)
    {
        status = string.IsNullOrEmpty(status) ? "PENDING_REVIEW" : status;
        ViewData["Title"] = "Publicaciones";
        return View(new ModerationViewModel((await admin.ListingsAsync(status, Math.Max(page, 1), 15, ct)).Data(), status));
    }

    [HttpPost]
    public async Task<IActionResult> Approve(Guid id, string? status, CancellationToken ct)
    {
        Report(await admin.ApproveListingAsync(id, ct), "Publicación aprobada y visible para el público.");
        return RedirectToAction(nameof(Index), new { status });
    }

    [HttpPost]
    public async Task<IActionResult> Reject(Guid id, string reason, string? status, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 5) this.Toast("error", "Indica un motivo de rechazo de al menos 5 caracteres.");
        else Report(await admin.RejectListingAsync(id, reason.Trim(), ct), "Publicación rechazada. Se notificó al propietario.");
        return RedirectToAction(nameof(Index), new { status });
    }
}

public sealed class NegotiationsController(IAdminApi admin) : AdminControllerBase
{
    public async Task<IActionResult> Index(string? status, int page = 1, CancellationToken ct = default)
    {
        ViewData["Title"] = "Negociaciones";
        ViewData["Status"] = status;
        return View((await admin.NegotiationsAsync(string.IsNullOrEmpty(status) ? null : status, Math.Max(page, 1), 15, ct)).Data());
    }
}

// =========================================================================== Planes, suscripciones y pagos
public sealed class PlanForm
{
    public int? Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal MonthlyPrice { get; set; }
    public int MaxActiveListings { get; set; }
    public int MaxPhotosPerVehicle { get; set; }
    public int MaxFeaturedListings { get; set; }
    public bool HasAdvancedStats { get; set; }
    public bool HasBusinessProfile { get; set; }
    public bool CanExportReports { get; set; }
    public bool HasPrioritySupport { get; set; }
    public int SortOrder { get; set; }
}

public sealed class PlansController(IAdminApi admin) : AdminControllerBase
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Planes";
        return View((await admin.PlansAsync(ct)).Data());
    }

    [HttpPost]
    public async Task<IActionResult> Save(PlanForm f, CancellationToken ct)
    {
        var r = await admin.UpsertPlanAsync(f.Id, new
        {
            code = f.Code?.Trim().ToUpperInvariant(), name = f.Name, monthlyPrice = f.MonthlyPrice, maxActiveListings = f.MaxActiveListings,
            maxPhotosPerVehicle = f.MaxPhotosPerVehicle, maxFeaturedListings = f.MaxFeaturedListings, hasAdvancedStats = f.HasAdvancedStats,
            hasBusinessProfile = f.HasBusinessProfile, canExportReports = f.CanExportReports, hasPrioritySupport = f.HasPrioritySupport, sortOrder = f.SortOrder
        }, ct);
        if (r.Success) this.Toast("success", f.Id is null ? "Plan creado." : "Plan actualizado. Los cambios aplican de inmediato a todos los suscriptores.");
        else this.Toast("error", r.Error!.FieldErrors.Count > 0 ? string.Join(" ", r.Error.FieldErrors.Values.SelectMany(v => v)) : r.Error.Message);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> SetActive(int id, bool active, CancellationToken ct)
    {
        Report(await admin.SetPlanActiveAsync(id, active, ct), active ? "Plan activado." : "Plan desactivado: ya no se ofrece a nuevos usuarios.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        Report(await admin.DeletePlanAsync(id, ct), "Plan eliminado.");
        return RedirectToAction(nameof(Index));
    }
}

/// <summary>Resuelve UserId → correo para listados administrativos (las suscripciones solo traen el id).</summary>
public sealed class UserDirectory(IAdminApi admin, IMemoryCache cache)
{
    public async Task<IReadOnlyDictionary<Guid, string>> LoadAsync(CancellationToken ct)
    {
        if (cache.TryGetValue("admin:users", out IReadOnlyDictionary<Guid, string>? hit) && hit is not null) return hit;
        var map = new Dictionary<Guid, string>();
        for (var page = 1; page <= 6; page++)
        {
            var r = await admin.UsersAsync(null, null, page, 50, ct);
            if (!r.Success) break;
            foreach (var u in r.Value!.Items) map[u.Id] = u.Email;
            if (page >= r.Value.TotalPages) break;
        }
        cache.Set("admin:users", (IReadOnlyDictionary<Guid, string>)map, TimeSpan.FromSeconds(20));
        return map;
    }
}

public sealed record SubscriptionsViewModel(PagedResult<SubscriptionInfo> Page, string? Status, IReadOnlyDictionary<Guid, string> Users, IReadOnlyList<PlanInfo> Plans);
public sealed record PaymentsViewModel(PagedResult<PaymentInfo> Page, bool? Simulated, IReadOnlyDictionary<Guid, string> Users, IReadOnlyList<SubscriptionInfo> Pending);

public sealed class SubscriptionsController(IAdminApi admin, UserDirectory directory) : AdminControllerBase
{
    public async Task<IActionResult> Index(string? status, int page = 1, CancellationToken ct = default)
    {
        var pageTask = admin.SubscriptionsAsync(string.IsNullOrEmpty(status) ? null : status, Math.Max(page, 1), 15, ct);
        var plansTask = admin.PlansAsync(ct);
        var usersTask = directory.LoadAsync(ct);
        await Task.WhenAll(pageTask, plansTask, usersTask);
        ViewData["Title"] = "Suscripciones";
        return View(new SubscriptionsViewModel(pageTask.Result.Data(), status, usersTask.Result, plansTask.Result.Data()));
    }

    [HttpPost]
    public async Task<IActionResult> Assign(Guid userId, string planCode, int months, CancellationToken ct)
    {
        Report(await admin.AssignPlanAsync(userId, planCode, Math.Clamp(months, 1, 36), ct), "Plan asignado y activado (otorgado por administración, sin pago).");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
    {
        Report(await admin.ActivateSubscriptionAsync(id, ct), "Suscripción activada administrativamente.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        Report(await admin.CancelSubscriptionAsync(id, ct), "Suscripción cancelada.");
        return RedirectToAction(nameof(Index));
    }
}

public sealed class PaymentsController(IAdminApi admin, UserDirectory directory) : AdminControllerBase
{
    public async Task<IActionResult> Index(bool? simulated, int page = 1, CancellationToken ct = default)
    {
        var pageTask = admin.PaymentsAsync(simulated, Math.Max(page, 1), 15, ct);
        var pendingTask = admin.SubscriptionsAsync("PENDING", 1, 50, ct);
        var usersTask = directory.LoadAsync(ct);
        await Task.WhenAll(pageTask, pendingTask, usersTask);
        ViewData["Title"] = "Pagos";
        return View(new PaymentsViewModel(pageTask.Result.Data(), simulated, usersTask.Result, pendingTask.Result.Success ? pendingTask.Result.Value!.Items : []));
    }

    [HttpPost]
    public async Task<IActionResult> Demo(Guid subscriptionId, CancellationToken ct)
    {
        Report(await admin.DemoPaymentAsync(subscriptionId, ct), "Pago de demostración registrado y plan activado. No representa dinero real.");
        return RedirectToAction(nameof(Index));
    }
}

// =========================================================================== Catálogos
public sealed record CatalogViewModel(string Tab, IReadOnlyList<BrandDto> Brands, IReadOnlyList<ModelDto> Models, IReadOnlyList<VehicleTypeDto> Types, string? Search, int? BrandId);

public sealed class CatalogController(IAdminApi admin) : AdminControllerBase
{
    private static readonly string[] Kinds = ["vehicle-types", "brands", "models"];

    public async Task<IActionResult> Index(string? tab, string? search, int? brandId, CancellationToken ct)
    {
        tab = tab is "models" or "types" ? tab : "brands";
        var brandsTask = admin.CatalogListAsync<BrandDto>("brands", null, ct);
        var typesTask = admin.CatalogListAsync<VehicleTypeDto>("vehicle-types", null, ct);
        var modelsTask = admin.CatalogListAsync<ModelDto>("models", brandId is null ? null : $"brandId={brandId}", ct);
        await Task.WhenAll(brandsTask, typesTask, modelsTask);

        var models = modelsTask.Result.Data().AsEnumerable();
        var brands = brandsTask.Result.Data().AsEnumerable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            models = models.Where(m => m.Name.Contains(search, StringComparison.OrdinalIgnoreCase));
            brands = brands.Where(b => b.Name.Contains(search, StringComparison.OrdinalIgnoreCase));
        }
        ViewData["Title"] = "Marcas y modelos";
        return View(new CatalogViewModel(tab, brands.ToList(), models.ToList(), typesTask.Result.Data(), search, brandId));
    }

    [HttpPost]
    public async Task<IActionResult> Save(string kind, int? id, string tab, string? name, string? code, int? brandId, int[]? vehicleTypeIds, CancellationToken ct)
    {
        if (!Kinds.Contains(kind)) return BadRequest();
        object body = kind switch
        {
            "brands" => new { name },
            "vehicle-types" => new { code = code?.Trim().ToUpperInvariant(), name },
            _ => new { brandId = brandId ?? 0, name, vehicleTypeIds = vehicleTypeIds ?? [] }
        };
        var r = await admin.CatalogUpsertAsync(kind, id, body, ct);
        if (r.Success) this.Toast("success", id is null ? "Registro creado." : "Registro actualizado.");
        else this.Toast("error", r.Error!.FieldErrors.Count > 0 ? string.Join(" ", r.Error.FieldErrors.Values.SelectMany(v => v)) : r.Error.Message);
        return RedirectToAction(nameof(Index), new { tab });
    }

    [HttpPost]
    public async Task<IActionResult> SetActive(string kind, int id, bool active, string tab, CancellationToken ct)
    {
        if (!Kinds.Contains(kind)) return BadRequest();
        Report(await admin.CatalogActiveAsync(kind, id, active, ct), active ? "Registro activado." : "Registro desactivado: deja de ofrecerse en los formularios públicos.");
        return RedirectToAction(nameof(Index), new { tab });
    }

    [HttpPost]
    public async Task<IActionResult> Delete(string kind, int id, string tab, CancellationToken ct)
    {
        if (!Kinds.Contains(kind)) return BadRequest();
        Report(await admin.CatalogDeleteAsync(kind, id, ct), "Registro eliminado.");
        return RedirectToAction(nameof(Index), new { tab });
    }
}

public sealed record LocationsViewModel(string Tab, IReadOnlyList<DepartmentDto> Departments, IReadOnlyList<ProvinceDto> Provinces, IReadOnlyList<DistrictDto> Districts,
    int? DepartmentId, int? ProvinceId, string? Search);

public sealed class LocationsController(IAdminApi admin) : AdminControllerBase
{
    private static readonly string[] Kinds = ["departments", "provinces", "districts"];

    public async Task<IActionResult> Index(string? tab, int? departmentId, int? provinceId, string? search, CancellationToken ct)
    {
        tab = tab is "provinces" or "districts" ? tab : "departments";
        var depsTask = admin.CatalogListAsync<DepartmentDto>("departments", null, ct);
        var provTask = admin.CatalogListAsync<ProvinceDto>("provinces", departmentId is null ? null : $"departmentId={departmentId}", ct);
        var distTask = admin.CatalogListAsync<DistrictDto>("districts", provinceId is null ? null : $"provinceId={provinceId}", ct);
        await Task.WhenAll(depsTask, provTask, distTask);
        bool Match(string n) => string.IsNullOrWhiteSpace(search) || n.Contains(search, StringComparison.OrdinalIgnoreCase);
        ViewData["Title"] = "Ubicaciones";
        return View(new LocationsViewModel(tab, depsTask.Result.Data().Where(d => Match(d.Name)).ToList(), provTask.Result.Data().Where(p => Match(p.Name)).ToList(),
            distTask.Result.Data().Where(d => Match(d.Name)).ToList(), departmentId, provinceId, search));
    }

    [HttpPost]
    public async Task<IActionResult> Save(string kind, int? id, string tab, string code, string name, int? parentId, CancellationToken ct)
    {
        if (!Kinds.Contains(kind)) return BadRequest();
        object body = kind switch
        {
            "departments" => new { code, name },
            "provinces" => new { departmentId = parentId ?? 0, code, name },
            _ => new { provinceId = parentId ?? 0, code, name }
        };
        var r = await admin.CatalogUpsertAsync(kind, id, body, ct);
        if (r.Success) this.Toast("success", id is null ? "Ubicación creada." : "Ubicación actualizada.");
        else this.Toast("error", r.Error!.FieldErrors.Count > 0 ? string.Join(" ", r.Error.FieldErrors.Values.SelectMany(v => v)) : r.Error.Message);
        return RedirectToAction(nameof(Index), new { tab });
    }

    [HttpPost]
    public async Task<IActionResult> SetActive(string kind, int id, bool active, string tab, CancellationToken ct)
    {
        if (!Kinds.Contains(kind)) return BadRequest();
        Report(await admin.CatalogActiveAsync(kind, id, active, ct), active ? "Ubicación activada." : "Ubicación desactivada.");
        return RedirectToAction(nameof(Index), new { tab });
    }

    [HttpPost]
    public async Task<IActionResult> Delete(string kind, int id, string tab, CancellationToken ct)
    {
        if (!Kinds.Contains(kind)) return BadRequest();
        Report(await admin.CatalogDeleteAsync(kind, id, ct), "Ubicación eliminada.");
        return RedirectToAction(nameof(Index), new { tab });
    }
}

// =========================================================================== Reportes y auditoría
public sealed record ReportsViewModel(PlanRevenueReport Revenue, IReadOnlyList<UsersByProfileTypeRow> Profiles);

public sealed class ReportsController(IAdminApi admin) : AdminControllerBase
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var revenueTask = admin.PlanRevenueAsync(ct);
        var profilesTask = admin.UsersByProfileTypeAsync(ct);
        await Task.WhenAll(revenueTask, profilesTask);
        ViewData["Title"] = "Reportes";
        return View(new ReportsViewModel(revenueTask.Result.Data(), profilesTask.Result.Data()));
    }

    [HttpGet]
    public async Task<IActionResult> Download(string report, CancellationToken ct)
    {
        if (report is not ("users.csv" or "subscriptions.csv")) return NotFound();
        var r = await admin.CsvAsync(report, ct);
        if (!r.Success) { this.ToastError(r.Error!); return RedirectToAction(nameof(Index)); }
        return File(r.Value.Bytes, "text/csv", $"motorlink-{report}");
    }
}

public sealed class AuditController(IAdminApi admin) : AdminControllerBase
{
    public async Task<IActionResult> Index(string? action, int page = 1, CancellationToken ct = default)
    {
        ViewData["Title"] = "Auditoría";
        ViewData["Action"] = action;
        return View((await admin.AuditAsync(action, Math.Max(page, 1), 20, ct)).Data());
    }
}
