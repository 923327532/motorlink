using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using MotorLink.Web.Extensions;
using MotorLink.Web.Services.Api;
using MotorLink.Web.ViewModels;

namespace MotorLink.Web.Areas.Dashboard.Controllers;

public sealed class FavoritesController(IListingsApi listings) : DashboardControllerBase
{
    public async Task<IActionResult> Index(int page = 1, CancellationToken ct = default)
    {
        ViewData["Title"] = "Favoritos";
        return View((await listings.FavoritesAsync(Math.Max(page, 1), 12, ct)).Data());
    }
}

public sealed record NegotiationsViewModel(PagedResult<NegotiationSummary> Page, string Role, string? Status);
public sealed record NegotiationPageViewModel(NegotiationDetail Detail, Guid MeId, bool IAmBuyer);

public sealed class StartNegotiationForm
{
    [Required] public Guid ListingId { get; set; }
    [Required] public string OperationType { get; set; } = "";
    public string? Periodicity { get; set; }
    [Range(0.01, 1_000_000_000, ErrorMessage = "Ingresa un monto mayor a cero.")] public decimal Amount { get; set; }
    [StringLength(1000)] public string? Message { get; set; }
}

public sealed class CounterForm
{
    [Range(0.01, 1_000_000_000, ErrorMessage = "Ingresa un monto mayor a cero.")] public decimal Amount { get; set; }
    [StringLength(1000)] public string? Message { get; set; }
}

public sealed class NegotiationsController(INegotiationsApi negotiations) : DashboardControllerBase
{
    public Task<IActionResult> Received(string? status, int page = 1, CancellationToken ct = default) => ListAsync("RECEIVED", status, page, ct);
    public Task<IActionResult> Sent(string? status, int page = 1, CancellationToken ct = default) => ListAsync("SENT", status, page, ct);

    private async Task<IActionResult> ListAsync(string role, string? status, int page, CancellationToken ct)
    {
        var r = (await negotiations.ListAsync(role, string.IsNullOrEmpty(status) ? null : status, Math.Max(page, 1), 10, ct)).Data();
        ViewData["Title"] = role == "RECEIVED" ? "Propuestas recibidas" : "Propuestas enviadas";
        return View("List", new NegotiationsViewModel(r, role, status));
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        var d = (await negotiations.GetAsync(id, ct)).Data();
        ViewData["Title"] = "Negociación";
        return View(new NegotiationPageViewModel(d, CurrentUserId, d.Negotiation.BuyerId == CurrentUserId));
    }

    /// <summary>Primera oferta del interesado (desde el detalle público de la publicación).</summary>
    [HttpPost]
    public async Task<IActionResult> Start(StartNegotiationForm form, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            this.Toast("error", ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage ?? "Revisa los datos de tu propuesta.");
            return Redirect($"/vehiculos/{form.ListingId}");
        }
        var r = await negotiations.StartAsync(new
        {
            listingId = form.ListingId, operationType = form.OperationType, amount = form.Amount,
            periodicity = form.OperationType == "SALE" ? null : form.Periodicity, message = form.Message
        }, ct);
        if (!r.Success)
        {
            this.Toast("error", r.Error!.Status == 409 ? r.Error.Message : r.Error.FieldErrors.Values.SelectMany(v => v).FirstOrDefault() ?? r.Error.Message);
            return Redirect($"/vehiculos/{form.ListingId}");
        }
        this.Toast("success", "Tu propuesta fue enviada. Te avisaremos cuando el propietario responda.");
        return RedirectToAction(nameof(Details), new { id = r.Value!.Negotiation.Id });
    }

    [HttpPost]
    public Task<IActionResult> Counter(Guid id, CounterForm form, CancellationToken ct) =>
        ActAsync(id, () => negotiations.CounterAsync(id, form.Amount, form.Message, ct), "Tu contrapropuesta fue enviada.", ModelState.IsValid);

    [HttpPost]
    public Task<IActionResult> Accept(Guid id, Guid offerId, CancellationToken ct) =>
        ActAsync(id, () => negotiations.AcceptAsync(id, offerId, ct), "Aceptaste la propuesta. Quedó registrado el precio acordado.", true);

    [HttpPost]
    public Task<IActionResult> Reject(Guid id, CancellationToken ct) => ActAsync(id, () => negotiations.RejectAsync(id, ct), "Rechazaste la propuesta.", true);

    [HttpPost]
    public Task<IActionResult> Cancel(Guid id, CancellationToken ct) => ActAsync(id, () => negotiations.CancelAsync(id, ct), "Cancelaste la negociación.", true);

    private async Task<IActionResult> ActAsync(Guid id, Func<Task<ApiResult<NegotiationDetail>>> action, string success, bool valid)
    {
        if (!valid) this.Toast("error", "Ingresa un monto válido mayor a cero.");
        else
        {
            var r = await action();
            if (r.Success) this.Toast("success", success); else this.ToastError(r.Error!);
        }
        return RedirectToAction(nameof(Details), new { id });
    }
}

public sealed class NotificationsController(IAccountApi account) : DashboardControllerBase
{
    public async Task<IActionResult> Index(bool unread = false, int page = 1, CancellationToken ct = default)
    {
        ViewData["Title"] = "Notificaciones";
        ViewData["Unread"] = unread;
        return View((await account.NotificationsAsync(unread, Math.Max(page, 1), 15, ct)).Data());
    }

    [HttpPost]
    public async Task<IActionResult> MarkAll(CancellationToken ct)
    {
        await account.MarkAllReadAsync(ct);
        this.Toast("success", "Marcaste todas las notificaciones como leídas.");
        return RedirectToAction(nameof(Index));
    }

    // ---- JSON para la campana del encabezado ----
    [HttpGet]
    public async Task<IActionResult> Count(CancellationToken ct) =>
        Json(new { count = (await account.UnreadCountAsync(ct)).Value?.Count ?? 0 });

    [HttpGet]
    public async Task<IActionResult> Latest(CancellationToken ct)
    {
        var r = await account.NotificationsAsync(false, 1, 6, ct);
        var items = (r.Success ? r.Value!.Items : []).Select(n => new { n.Id, n.Title, n.Message, n.IsRead, url = TargetUrl(n), when = Ui.TimeAgo(n.CreatedAt) });
        return Json(items);
    }

    /// <summary>Marca como leída y navega al recurso relacionado.</summary>
    [HttpGet]
    public async Task<IActionResult> Open(Guid id, string? to, CancellationToken ct)
    {
        await account.MarkReadAsync(id, ct);
        return LocalRedirect(Url.IsLocalUrl(to) ? to! : "/panel/notifications");
    }

    [HttpPost("/panel/notifications/read-all")]
    public async Task<IActionResult> ReadAll(CancellationToken ct)
    {
        await account.MarkAllReadAsync(ct);
        return Json(new { ok = true });
    }

    private static string TargetUrl(NotificationInfo n)
    {
        var to = n.Type switch
        {
            var t when t.StartsWith("NEGOTIATION") && n.RelatedEntityId is not null => $"/panel/negotiations/details/{n.RelatedEntityId}",
            var t when t.StartsWith("LISTING") => "/panel/listings",
            var t when t.StartsWith("SUBSCRIPTION") => "/panel/subscription",
            _ => "/panel/notifications"
        };
        return $"/panel/notifications/open/{n.Id}?to={Uri.EscapeDataString(to)}";
    }
}

public sealed class ProfileController(IUsersApi users, ICatalogApi catalogs) : DashboardControllerBase
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Mi perfil";
        return View(ToVm((await users.GetProfileAsync(ct)).Data()));
    }

    [HttpPost]
    public async Task<IActionResult> Index(ProfileViewModel model, CancellationToken ct)
    {
        ViewData["Title"] = "Mi perfil";
        if (!ModelState.IsValid) return View(model);
        var r = await users.UpdateProfileAsync(new
        {
            firstName = model.FirstName, lastName = model.LastName, phoneNumber = model.PhoneNumber, documentNumber = model.DocumentNumber,
            profileType = model.ProfileType, businessName = model.ProfileType == "BUSINESS" ? model.BusinessName : null,
            ruc = model.ProfileType == "BUSINESS" ? model.Ruc : null, businessDescription = model.BusinessDescription, website = model.Website
        }, ct);
        if (!r.Success)
        {
            this.AddApiErrors(r.Error!);
            return View(model);
        }
        this.Toast("success", "Tu perfil fue actualizado.");
        return RedirectToAction(nameof(Index));
    }

    private static ProfileViewModel ToVm(ApiProfile p) => new()
    {
        Email = p.Email, FirstName = p.FirstName, LastName = p.LastName, PhoneNumber = p.PhoneNumber, DocumentNumber = p.DocumentNumber,
        ProfileType = p.ProfileType, BusinessName = p.Business?.BusinessName, Ruc = p.Business?.Ruc,
        BusinessDescription = p.Business?.Description, Website = p.Business?.Website
    };
}

public sealed class SecurityController(IAuthApi auth, Services.Sessions.IWebSessionService sessions) : DashboardControllerBase
{
    public IActionResult Index() { ViewData["Title"] = "Seguridad"; return View(new ChangePasswordViewModel()); }

    [HttpPost]
    public async Task<IActionResult> Index(ChangePasswordViewModel model, CancellationToken ct)
    {
        ViewData["Title"] = "Seguridad";
        if (!ModelState.IsValid) return View(new ChangePasswordViewModel());
        var r = await auth.ChangePasswordAsync(model.CurrentPassword, model.NewPassword, ct);
        if (!r.Success)
        {
            this.AddApiErrors(r.Error!, "");
            foreach (var key in new[] { "CurrentPassword", "NewPassword" }) { /* los errores por campo ya se asociaron */ }
            return View(new ChangePasswordViewModel());
        }
        // La API revocó todas las sesiones: se cierra la local y se pide iniciar sesión de nuevo.
        await sessions.SignOutAsync(HttpContext, revokeOnApi: false, ct);
        this.Toast("success", "Contraseña actualizada. Por seguridad, inicia sesión nuevamente.");
        return Redirect("/ingresar");
    }
}
