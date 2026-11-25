using Microsoft.AspNetCore.Mvc;
using MotorLink.Web.Services.Api;

namespace MotorLink.Web.ViewComponents;

/// <summary>Campana de notificaciones del encabezado (contador real desde la API; el listado se carga al abrir el menú).</summary>
public sealed class NotificationBellViewComponent(IAccountApi account) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync(string basePath = "/panel/notifications", string allUrl = "/panel/notifications")
    {
        var count = 0;
        try
        {
            var r = await account.UnreadCountAsync(HttpContext.RequestAborted);
            if (r.Success) count = r.Value!.Count;
        }
        catch (Exception ex) when (ex is ApiUnauthorizedException or ApiForbiddenException) { /* el filtro de la acción gestiona la sesión */ }
        ViewData["BasePath"] = basePath;
        ViewData["AllUrl"] = allUrl;
        return View(count);
    }
}

/// <summary>Paginación que conserva el resto de parámetros de la URL.</summary>
public sealed class PaginationViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(int page, int totalPages, string parameter = "page")
    {
        var query = Request.Query.Where(q => q.Key != parameter && q.Key != "__RequestVerificationToken")
            .SelectMany(q => q.Value.Select(v => (q.Key, Value: v ?? ""))).ToList();

        string Link(int p)
        {
            var parts = query.Select(q => $"{Uri.EscapeDataString(q.Key)}={Uri.EscapeDataString(q.Value)}").ToList();
            if (p > 1) parts.Add($"{parameter}={p}");
            return Request.Path + (parts.Count > 0 ? "?" + string.Join('&', parts) : "");
        }

        // Ventana de páginas: 1 … (p-2..p+2) … n
        var pages = new SortedSet<int> { 1, totalPages };
        for (var i = page - 2; i <= page + 2; i++) if (i >= 1 && i <= totalPages) pages.Add(i);
        var items = new List<(string Text, string? Url, bool Active, bool Disabled)>
        {
            ("Anterior", page > 1 ? Link(page - 1) : null, false, page <= 1)
        };
        var prev = 0;
        foreach (var p in pages)
        {
            if (p - prev > 1) items.Add(("…", null, false, true));
            items.Add((p.ToString(), Link(p), p == page, false));
            prev = p;
        }
        items.Add(("Siguiente", page < totalPages ? Link(page + 1) : null, false, page >= totalPages));
        return View(items);
    }
}

/// <summary>Tarjeta de publicación (marketplace, inicio, favoritos).</summary>
public sealed class ListingCardViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(ListingSummary listing, bool isFavorite = false, string? preferred = null, bool removeOnUnfavorite = false)
    {
        ViewData["IsFavorite"] = isFavorite;
        ViewData["Preferred"] = preferred;
        ViewData["Remove"] = removeOnUnfavorite;
        return View(listing);
    }
}
