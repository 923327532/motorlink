using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using MotorLink.Web.Handlers;
using MotorLink.Web.Services.Api;

namespace MotorLink.Web.Services.Sessions;

/// <summary>Crea y destruye la sesión web: tokens en el servidor (ISessionStore) y solo una cookie opaca en el navegador.</summary>
public interface IWebSessionService
{
    Task SignInAsync(HttpContext http, ApiAuthResponse auth, CancellationToken ct);
    /// <param name="revokeOnApi">true revoca el refresh token en la API (cierre voluntario de sesión).</param>
    Task SignOutAsync(HttpContext http, bool revokeOnApi, CancellationToken ct);
}

public sealed class WebSessionService(ISessionStore store, IAuthApi auth) : IWebSessionService
{
    public async Task SignInAsync(HttpContext http, ApiAuthResponse response, CancellationToken ct)
    {
        // Evita fijación de sesión: siempre se emite un identificador nuevo.
        if (http.User.FindFirst(BearerTokenHandler.SessionClaim)?.Value is { } old) await store.DeleteAsync(old, ct);

        var sid = await store.CreateAsync(new SessionTokens(response.AccessToken, response.AccessTokenExpiresAt, response.RefreshToken, response.RefreshTokenExpiresAt), ct);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, response.User.Id.ToString()),
            new(ClaimTypes.Name, response.User.FullName),
            new(ClaimTypes.Email, response.User.Email),
            new(BearerTokenHandler.SessionClaim, sid)
        };
        claims.AddRange(response.User.Roles.Select(r => new Claim(ClaimTypes.Role, r)));

        await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)),
            new AuthenticationProperties { IsPersistent = true, ExpiresUtc = response.RefreshTokenExpiresAt });
    }

    public async Task SignOutAsync(HttpContext http, bool revokeOnApi, CancellationToken ct)
    {
        if (http.User.FindFirst(BearerTokenHandler.SessionClaim)?.Value is { } sid)
        {
            if (revokeOnApi && await store.GetAsync(sid, ct) is { } tokens)
                await auth.LogoutAsync(tokens.RefreshToken, ct);
            await store.DeleteAsync(sid, ct);
        }
        await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }
}
