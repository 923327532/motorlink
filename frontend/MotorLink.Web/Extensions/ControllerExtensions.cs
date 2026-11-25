using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using MotorLink.Web.Services.Api;

namespace MotorLink.Web.Extensions;

public sealed record ToastMessage(string Type, string Message);

public static class ControllerExtensions
{
    private const string Key = "ml.toasts";

    /// <summary>Encola una notificación visual que se muestra en la siguiente página renderizada (sobrevive a redirecciones).</summary>
    public static void Toast(this Controller c, string type, string message)
    {
        var list = c.TempData.Peek(Key) is string s ? JsonSerializer.Deserialize<List<ToastMessage>>(s) ?? [] : [];
        list.Add(new ToastMessage(type, message));
        c.TempData[Key] = JsonSerializer.Serialize(list);
    }

    public static IReadOnlyList<ToastMessage> TakeToasts(this ITempDataDictionary t) =>
        t[Key] is string s ? JsonSerializer.Deserialize<List<ToastMessage>>(s) ?? [] : [];

    public static void ToastError(this Controller c, ApiError e) => c.Toast("error", e.Message);

    /// <summary>Copia los errores por campo de la API al ModelState para mostrarlos junto a cada control.</summary>
    public static void AddApiErrors(this Controller c, ApiError error, string prefix = "")
    {
        foreach (var (field, messages) in error.FieldErrors)
            foreach (var m in messages)
                c.ModelState.AddModelError(string.IsNullOrEmpty(prefix) ? field : $"{prefix}.{field}", m);
        if (error.FieldErrors.Count == 0) c.ModelState.AddModelError(string.Empty, error.Message);
    }

    public static bool IsAjax(this HttpRequest r) =>
        r.Headers.XRequestedWith == "XMLHttpRequest" || r.Headers.Accept.ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase);

    public static Guid? UserId(this ClaimsPrincipal p) => Guid.TryParse(p.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
