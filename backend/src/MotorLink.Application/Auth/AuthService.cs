using Microsoft.Extensions.Options;
using MotorLink.Application.Abstractions;
using MotorLink.Application.Options;
using MotorLink.Application.Subscriptions;
using MotorLink.Domain.Common;
using MotorLink.Domain.Entities;
using MotorLink.Domain.Exceptions;

namespace MotorLink.Application.Auth;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, string? ip, CancellationToken ct);
    Task<AuthResponse> LoginAsync(LoginRequest request, string? ip, CancellationToken ct);
    Task<AuthResponse> RefreshAsync(RefreshRequest request, string? ip, CancellationToken ct);
    Task LogoutAsync(LogoutRequest request, CancellationToken ct);
    Task<UserDto> GetCurrentUserAsync(Guid userId, CancellationToken ct);
    Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken ct);
    Task<IReadOnlyList<UserDto>> ListUsersAsync(int page, int pageSize, CancellationToken ct);
}

public sealed class AuthService(
    IIdentityService identity,
    ITokenService tokens,
    IRefreshTokenRepository refreshTokens,
    ISubscriptionPermissions subscriptions,
    IOptions<JwtOptions> jwtOptions,
    TimeProvider clock) : IAuthService
{
    private const string InvalidCredentials = "Credenciales inválidas.";
    private readonly JwtOptions _jwt = jwtOptions.Value;

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, string? ip, CancellationToken ct)
    {
        var user = await identity.CreateUserAsync(request, ct);
        await subscriptions.EnsureFreeSubscriptionAsync(user.Id, ct); // plan gratuito automático
        return await IssueTokensAsync(user, Guid.NewGuid(), ip, ct);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, string? ip, CancellationToken ct)
    {
        var user = await identity.FindByEmailAsync(request.Email, ct);
        if (user is null || !user.IsActive)
            throw new AuthenticationFailedException(InvalidCredentials);

        switch (await identity.CheckPasswordAsync(user.Id, request.Password, ct))
        {
            case PasswordCheckResult.LockedOut:
                throw new AccountLockedException("La cuenta está bloqueada temporalmente por demasiados intentos fallidos.");
            case PasswordCheckResult.Failed:
                throw new AuthenticationFailedException(InvalidCredentials);
        }

        return await IssueTokensAsync(user, Guid.NewGuid(), ip, ct);
    }

    /// <summary>
    /// Rotación: cada refresh invalida el token usado y emite uno nuevo en la misma familia.
    /// Si se presenta un token ya revocado se asume robo y se revoca toda la familia.
    /// </summary>
    public async Task<AuthResponse> RefreshAsync(RefreshRequest request, string? ip, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var stored = await refreshTokens.FindByHashAsync(tokens.HashToken(request.RefreshToken), ct)
            ?? throw new AuthenticationFailedException("Refresh token inválido.");

        if (stored.IsRevoked)
        {
            await refreshTokens.RevokeFamilyAsync(stored.FamilyId, RefreshTokenRevocationReason.ReuseDetected, now, ct);
            await refreshTokens.SaveChangesAsync(ct);
            throw new AuthenticationFailedException("Refresh token reutilizado. La sesión fue revocada.");
        }

        if (stored.IsExpired(now))
            throw new AuthenticationFailedException("Refresh token expirado.");

        var user = await identity.FindByIdAsync(stored.UserId, ct);
        if (user is null || !user.IsActive)
            throw new AuthenticationFailedException("Refresh token inválido.");

        return await IssueTokensAsync(user, stored.FamilyId, ip, ct, stored);
    }

    public async Task LogoutAsync(LogoutRequest request, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var stored = await refreshTokens.FindByHashAsync(tokens.HashToken(request.RefreshToken), ct);
        if (stored is null) return; // logout idempotente: no revela si el token existía

        if (request.AllSessions)
            await refreshTokens.RevokeAllForUserAsync(stored.UserId, RefreshTokenRevocationReason.Logout, now, ct);
        else
            await refreshTokens.RevokeFamilyAsync(stored.FamilyId, RefreshTokenRevocationReason.Logout, now, ct);

        await refreshTokens.SaveChangesAsync(ct);
    }

    public async Task<UserDto> GetCurrentUserAsync(Guid userId, CancellationToken ct)
    {
        var user = await identity.FindByIdAsync(userId, ct)
            ?? throw new NotFoundException("Usuario no encontrado.");
        return user.ToDto();
    }

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken ct)
    {
        await identity.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword, ct);
        // Tras cambiar la contraseña todas las sesiones existentes dejan de ser válidas.
        await refreshTokens.RevokeAllForUserAsync(userId, RefreshTokenRevocationReason.PasswordChanged,
            clock.GetUtcNow().UtcDateTime, ct);
        await refreshTokens.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<UserDto>> ListUsersAsync(int page, int pageSize, CancellationToken ct)
    {
        var users = await identity.ListUsersAsync(Math.Max(page, 1), Math.Clamp(pageSize, 1, 100), ct);
        return users.Select(u => u.ToDto()).ToList();
    }

    private async Task<AuthResponse> IssueTokensAsync(
        UserAccount user, Guid familyId, string? ip, CancellationToken ct, RefreshToken? rotating = null)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var access = tokens.CreateAccessToken(user);

        var rawRefresh = tokens.GenerateRefreshToken();
        var newToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            FamilyId = familyId,
            TokenHash = tokens.HashToken(rawRefresh),
            CreatedAt = now,
            ExpiresAt = now.AddDays(_jwt.RefreshTokenDays),
            CreatedByIp = ip
        };

        rotating?.Revoke(now, RefreshTokenRevocationReason.Rotated, newToken.TokenHash);
        refreshTokens.Add(newToken);
        await refreshTokens.SaveChangesAsync(ct); // un solo SaveChanges: rotación atómica

        return new AuthResponse(access.Token, access.ExpiresAt, rawRefresh, newToken.ExpiresAt, user.ToDto());
    }
}
