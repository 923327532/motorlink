using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MotorLink.Web.Extensions;
using MotorLink.Web.Services.Api;
using MotorLink.Web.Services.Sessions;
using MotorLink.Web.ViewModels;

namespace MotorLink.Web.Controllers;

/// <summary>
/// Intermediario entre el navegador y la API: el navegador solo recibe una cookie de sesión (HttpOnly/SameSite/Secure);
/// los JWT y refresh tokens quedan en el servidor.
/// </summary>
[AllowAnonymous]
public sealed class AuthController(IAuthApi auth, IWebSessionService sessions) : Controller
{
    [HttpGet("ingresar")]
    public IActionResult Login(string? returnUrl = null, bool expired = false)
    {
        if (User.Identity?.IsAuthenticated == true) return Redirect(Safe(returnUrl));
        if (expired) this.Toast("warning", "Tu sesión expiró. Inicia sesión nuevamente para continuar.");
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost("ingresar")]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(model);

        var result = await auth.LoginAsync(model.Email.Trim(), model.Password, ct);
        if (!result.Success)
        {
            // Mensaje seguro: no revela si el correo existe. 423/429 muestran el detalle de la API (bloqueo temporal, límite de intentos).
            ModelState.AddModelError(string.Empty, result.Error!.Status is 423 or 429 or 503 ? result.Error.Message : "Correo o contraseña incorrectos.");
            model.Password = string.Empty;
            return View(model);
        }

        await sessions.SignInAsync(HttpContext, result.Value!, ct);
        this.Toast("success", $"Bienvenido, {result.Value!.User.FullName}.");
        var target = Safe(model.ReturnUrl, result.Value.User.Roles.Contains("ADMIN") ? "/admin" : "/panel");
        return Redirect(target);
    }

    [HttpGet("registro")]
    public IActionResult Register(string? returnUrl = null, string? type = null) =>
        User.Identity?.IsAuthenticated == true ? Redirect("/panel")
            : View(new RegisterViewModel { ReturnUrl = returnUrl, ProfileType = type?.ToUpperInvariant() == "BUSINESS" ? "BUSINESS" : "PERSONAL" });

    [HttpPost("registro")]
    public async Task<IActionResult> Register(RegisterViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return Fail(model);

        var result = await auth.RegisterAsync(new
        {
            email = model.Email.Trim(), password = model.Password, firstName = model.FirstName, lastName = model.LastName,
            phoneNumber = model.PhoneNumber, profileType = model.ProfileType, businessName = model.ProfileType == "BUSINESS" ? model.BusinessName : null
        }, ct);

        if (!result.Success)
        {
            foreach (var (field, messages) in result.Error!.FieldErrors)
                foreach (var m in messages)
                    ModelState.AddModelError(ModelState.ContainsKey(field) ? field : string.Empty, m);
            if (result.Error.FieldErrors.Count == 0)
                ModelState.AddModelError(string.Empty, result.Error.Status == 409 ? "Ya existe una cuenta con ese correo electrónico." : result.Error.Message);
            return Fail(model);
        }

        await sessions.SignInAsync(HttpContext, result.Value!, ct);
        this.Toast("success", "Tu cuenta fue creada. Ya tienes el plan Gratis activo.");
        return Redirect(Safe(model.ReturnUrl, "/panel"));
    }

    [HttpPost("salir"), Authorize]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        await sessions.SignOutAsync(HttpContext, revokeOnApi: true, ct);
        this.Toast("info", "Cerraste sesión correctamente.");
        return Redirect("/");
    }

    [HttpGet("acceso-denegado")]
    public IActionResult AccessDenied() { Response.StatusCode = 403; return View(); }

    private IActionResult Fail(RegisterViewModel m) { m.Password = m.ConfirmPassword = string.Empty; return View(m); }

    /// <summary>Evita redirecciones abiertas: solo URLs locales.</summary>
    private string Safe(string? returnUrl, string fallback = "/") => Url.IsLocalUrl(returnUrl) ? returnUrl! : fallback;
}
