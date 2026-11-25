using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MotorLink.Web.Extensions;
using MotorLink.Web.Services.Api;

namespace MotorLink.Web.Areas.Dashboard.Controllers;

/// <summary>Base de los controladores del panel: área Dashboard y sesión obligatoria.</summary>
[Area("Dashboard"), Authorize]
public abstract class DashboardControllerBase : Controller
{
    protected Guid CurrentUserId => User.UserId() ?? Guid.Empty;
}

public sealed record DashboardHomeViewModel(
    DashboardData Data, IReadOnlyList<MyListing> Listings, IReadOnlyList<NegotiationSummary> Pending, CurrentPlan Plan, DateTime From, DateTime To);

public sealed class HomeController(IAccountApi account, IListingsApi listings, INegotiationsApi negotiations, IBillingApi billing) : DashboardControllerBase
{
    public async Task<IActionResult> Index(DateTime? from, DateTime? to, CancellationToken ct)
    {
        // El rango se interpreta como días completos (UTC). Por defecto: últimos 30 días.
        var end = (to?.Date ?? DateTime.UtcNow.Date).AddDays(1).AddTicks(-1);
        var start = (from?.Date ?? end.Date.AddDays(-30));
        if (start > end) { this.Toast("warning", "La fecha inicial no puede ser posterior a la final."); (start, end) = (end.Date.AddDays(-30), end); }

        var dataTask = account.DashboardAsync(DateTime.SpecifyKind(start, DateTimeKind.Utc), DateTime.SpecifyKind(end, DateTimeKind.Utc), ct);
        var listingsTask = listings.MineAsync(null, 1, 50, ct);
        var pendingTask = negotiations.ListAsync("ALL", null, 1, 50, ct);
        var planTask = billing.CurrentAsync(ct);
        await Task.WhenAll(dataTask, listingsTask, pendingTask, planTask);

        if (!dataTask.Result.Success && dataTask.Result.Error!.Status == 400)
        {
            this.Toast("error", dataTask.Result.Error.Message);
            return RedirectToAction(nameof(Index));
        }
        var data = dataTask.Result.Data();
        var open = new[] { "PENDING", "COUNTER_OFFER" };
        var pending = pendingTask.Result.Success
            ? pendingTask.Result.Value!.Items.Where(n => open.Contains(n.Status) && n.LastOfferById != CurrentUserId).Take(5).ToList()
            : [];

        ViewData["Title"] = "Resumen";
        return View(new DashboardHomeViewModel(data, listingsTask.Result.Success ? listingsTask.Result.Value!.Items : [], pending, planTask.Result.Data(), start, end));
    }

    [HttpGet]
    public async Task<IActionResult> Export(DateTime? from, DateTime? to, CancellationToken ct)
    {
        var r = await account.ExportAsync(from, to, ct);
        if (!r.Success)
        {
            this.Toast("error", r.Error!.Status == 403 ? "Tu plan no incluye la exportación de reportes. Mejora tu plan para usarla." : r.Error.Message);
            return RedirectToAction(nameof(Index));
        }
        return File(r.Value.Bytes, "text/csv", $"motorlink-resumen-{DateTime.UtcNow:yyyyMMdd}.csv");
    }
}
