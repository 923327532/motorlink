using System.Net.Http.Headers;
using System.Net.Http.Json;
using MotorLink.Web.Services.Api;
using MotorLink.Web.Services.Sessions;

namespace MotorLink.Web.Handlers;

public interface ITokenProvider
{
    /// <summary>Devuelve un access token vigente (renovándolo si es necesario) o null si la sesión ya no es válida.</summary>
    Task<string?> GetAccessTokenAsync(string sessionId, CancellationToken ct);
}

/// <summary>
/// Renueva el access token usando el refresh token de la sesión. Como la API rota el refresh token en cada uso y
/// detecta reutilización, las renovaciones concurrentes de una misma sesión se serializan con un bloqueo.
/// </summary>
public sealed class TokenProvider(
    ISessionStore store, IHttpClientFactory httpFactory, TimeProvider clock, ILogger<TokenProvider> logger) : ITokenProvider
{
    public const string AnonymousClientName = "MotorLinkApi.Anonymous";
    private static readonly TimeSpan Skew = TimeSpan.FromSeconds(30);
    private static readonly SemaphoreSlim[] Locks = Enumerable.Range(0, 64).Select(_ => new SemaphoreSlim(1, 1)).ToArray();

    public async Task<string?> GetAccessTokenAsync(string sessionId, CancellationToken ct)
    {
        var tokens = await store.GetAsync(sessionId, ct);
        if (tokens is null) return null;
        if (IsFresh(tokens)) return tokens.AccessToken;

        var gate = Locks[(uint)sessionId.GetHashCode() % Locks.Length];
        await gate.WaitAsync(ct);
        try
        {
            tokens = await store.GetAsync(sessionId, ct); // otro request pudo renovar mientras esperábamos
            if (tokens is null) return null;
            if (IsFresh(tokens)) return tokens.AccessToken;

            var http = httpFactory.CreateClient(AnonymousClientName);
            using var response = await http.PostAsJsonAsync("api/v1/auth/refresh", new { refreshToken = tokens.RefreshToken }, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogInformation("Renovación de sesión rechazada por la API ({Status}); se cierra la sesión local.", (int)response.StatusCode);
                await store.DeleteAsync(sessionId, ct);
                return null;
            }

            var auth = await response.Content.ReadFromJsonAsync<ApiAuthResponse>(ct);
            if (auth is null) return null;
            var renewed = new SessionTokens(auth.AccessToken, auth.AccessTokenExpiresAt, auth.RefreshToken, auth.RefreshTokenExpiresAt);
            await store.UpdateAsync(sessionId, renewed, ct);
            return renewed.AccessToken;
        }
        finally
        {
            gate.Release();
        }
    }

    private bool IsFresh(SessionTokens t) => t.AccessTokenExpiresAt - clock.GetUtcNow().UtcDateTime > Skew;
}

/// <summary>Adjunta el Bearer de la sesión actual y reenvía la IP del cliente a la API (rate limiting por IP real).</summary>
public sealed class BearerTokenHandler(IHttpContextAccessor accessor, ITokenProvider tokens) : DelegatingHandler
{
    public const string SessionClaim = "sid";

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var ctx = accessor.HttpContext;
        if (ctx is not null)
        {
            if (ctx.User.FindFirst(SessionClaim)?.Value is { } sid && await tokens.GetAccessTokenAsync(sid, ct) is { } access)
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);

            if (ctx.Connection.RemoteIpAddress is { } ip)
                request.Headers.TryAddWithoutValidation("X-Forwarded-For", ip.ToString());
        }
        return await base.SendAsync(request, ct);
    }
}
