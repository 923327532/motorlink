using Microsoft.AspNetCore.Mvc;
using MotorLink.Web.Extensions;
using MotorLink.Web.Services.Api;

namespace MotorLink.Web.Areas.Dashboard.Controllers;

public sealed record SubscriptionViewModel(CurrentPlan Current, IReadOnlyList<PlanInfo> Plans, PagedResult<SubscriptionInfo> History, SubscriptionInfo? Pending);
public sealed record CheckoutViewModel(SubscriptionInfo Subscription, PlanInfo Plan);

/// <summary>Suscripción del usuario. El pago es SIMULADO (flujo de demostración): no se piden tarjetas ni se cobra dinero.</summary>
public sealed class SubscriptionController(IBillingApi billing) : DashboardControllerBase
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var cTask = billing.CurrentAsync(ct);
        var pTask = billing.PlansAsync(ct);
        var hTask = billing.HistoryAsync(1, 20, ct);
        await Task.WhenAll(cTask, pTask, hTask);
        var history = hTask.Result.Data();
        ViewData["Title"] = "Mi suscripción";
        return View(new SubscriptionViewModel(cTask.Result.Data(), pTask.Result.Data(), history, history.Items.FirstOrDefault(s => s.Status == "PENDING")));
    }

    public async Task<IActionResult> Payments(int page = 1, CancellationToken ct = default)
    {
        ViewData["Title"] = "Historial de pagos";
        return View((await billing.PaymentsAsync(Math.Max(page, 1), 15, ct)).Data());
    }

    [HttpPost]
    public async Task<IActionResult> Select(string planCode, CancellationToken ct)
    {
        var r = await billing.ChangePlanAsync(planCode, ct);
        if (!r.Success) { this.ToastError(r.Error!); return Redirect("/planes"); }
        if (r.Value!.Status == "PENDING") return RedirectToAction(nameof(Checkout), new { id = r.Value.Id });
        this.Toast("success", "Actualizamos tu suscripción.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Renew(CancellationToken ct)
    {
        var r = await billing.RenewAsync(ct);
        if (!r.Success) { this.ToastError(r.Error!); return RedirectToAction(nameof(Index)); }
        return RedirectToAction(nameof(Checkout), new { id = r.Value!.Id });
    }

    public async Task<IActionResult> Checkout(Guid id, CancellationToken ct)
    {
        var historyTask = billing.HistoryAsync(1, 50, ct);
        var plansTask = billing.PlansAsync(ct);
        await Task.WhenAll(historyTask, plansTask);
        var sub = historyTask.Result.Data().Items.FirstOrDefault(s => s.Id == id);
        if (sub is null || sub.Status != "PENDING") { this.Toast("info", "Esa solicitud ya no está pendiente."); return RedirectToAction(nameof(Index)); }
        var plan = plansTask.Result.Data().First(p => p.Code == sub.PlanCode);
        ViewData["Title"] = "Confirmar suscripción";
        return View(new CheckoutViewModel(sub, plan));
    }

    [HttpPost]
    public async Task<IActionResult> Pay(Guid id, CancellationToken ct)
    {
        var r = await billing.PayAsync(id, ct);
        if (!r.Success) { this.ToastError(r.Error!); return RedirectToAction(nameof(Checkout), new { id }); }
        this.Toast("success", $"Tu plan {r.Value!.PlanName} está activo (pago de demostración).");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        var r = await billing.CancelAsync(id, ct);
        if (r.Success) this.Toast("success", r.Value!.Status == "CANCELLED" ? "Solicitud cancelada." : "No se renovará tu plan: seguirá activo hasta su vencimiento.");
        else this.ToastError(r.Error!);
        return RedirectToAction(nameof(Index));
    }
}
