using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MotorLink.API.Authorization;
using MotorLink.Application.Auth;

namespace MotorLink.API.Controllers;

/// <summary>Registro, sesión y credenciales.</summary>
[ApiController]
[Route("api/v1/auth")]
[Produces("application/json")]
public sealed class AuthController(IAuthService auth) : ControllerBase
{
    private string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();

    /// <summary>Registra una cuenta (PERSONAL o BUSINESS) e inicia sesión.</summary>
    [HttpPost("register"), AllowAnonymous, EnableRateLimiting(Policies.AuthRateLimit)]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await auth.RegisterAsync(request, ClientIp, ct));

    /// <summary>Inicia sesión y devuelve access token (15 min) y refresh token (7 días).</summary>
    [HttpPost("login"), AllowAnonymous, EnableRateLimiting(Policies.AuthRateLimit)]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status423Locked)]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken ct) =>
        await auth.LoginAsync(request, ClientIp, ct);

    /// <summary>Rota el refresh token y emite un nuevo par de tokens. Un token reutilizado revoca la sesión completa.</summary>
    [HttpPost("refresh"), AllowAnonymous, EnableRateLimiting(Policies.AuthRateLimit)]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshRequest request, CancellationToken ct) =>
        await auth.RefreshAsync(request, ClientIp, ct);

    /// <summary>Revoca la sesión asociada al refresh token (o todas con allSessions=true). Idempotente.</summary>
    [HttpPost("logout"), AllowAnonymous, EnableRateLimiting(Policies.AuthRateLimit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(LogoutRequest request, CancellationToken ct)
    {
        await auth.LogoutAsync(request, ct);
        return NoContent();
    }

    /// <summary>Usuario autenticado actual.</summary>
    [HttpGet("me")]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserDto>> Me(CancellationToken ct) =>
        await auth.GetCurrentUserAsync(User.GetUserId(), ct);

    /// <summary>Cambia la contraseña y revoca todas las sesiones existentes.</summary>
    [HttpPost("change-password"), EnableRateLimiting(Policies.AuthRateLimit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        await auth.ChangePasswordAsync(User.GetUserId(), request, ct);
        return NoContent();
    }
}
