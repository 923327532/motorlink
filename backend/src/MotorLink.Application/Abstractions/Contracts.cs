using MotorLink.Application.Auth;
using MotorLink.Domain.Common;
using MotorLink.Domain.Entities;

namespace MotorLink.Application.Abstractions;

public sealed record UserAccount(Guid Id, string Email, string FullName, bool IsActive, IReadOnlyList<string> Roles)
{
    public UserDto ToDto() => new(Id, Email, FullName, Roles);
}

public enum PasswordCheckResult { Success, Failed, LockedOut }

public sealed record AccessToken(string Token, DateTime ExpiresAt);

/// <summary>Gestión de cuentas (implementada sobre ASP.NET Core Identity).</summary>
public interface IIdentityService
{
    /// <summary>Crea usuario + perfil + rol USER. Lanza ConflictException o BusinessValidationException.</summary>
    Task<UserAccount> CreateUserAsync(RegisterRequest request, CancellationToken ct);
    Task<UserAccount?> FindByEmailAsync(string email, CancellationToken ct);
    Task<UserAccount?> FindByIdAsync(Guid userId, CancellationToken ct);
    /// <summary>Verifica la contraseña aplicando el contador de bloqueo de cuenta.</summary>
    Task<PasswordCheckResult> CheckPasswordAsync(Guid userId, string password, CancellationToken ct);
    Task ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken ct);
    Task<IReadOnlyList<UserAccount>> ListUsersAsync(int page, int pageSize, CancellationToken ct);
}

public interface ITokenService
{
    AccessToken CreateAccessToken(UserAccount user);
    /// <summary>Token opaco criptográficamente seguro (64 bytes aleatorios, base64url).</summary>
    string GenerateRefreshToken();
    /// <summary>SHA-256 hexadecimal del token.</summary>
    string HashToken(string token);
}

public interface IRefreshTokenRepository
{
    void Add(RefreshToken token);
    Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken ct);
    Task RevokeFamilyAsync(Guid familyId, RefreshTokenRevocationReason reason, DateTime utcNow, CancellationToken ct);
    Task RevokeAllForUserAsync(Guid userId, RefreshTokenRevocationReason reason, DateTime utcNow, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}

public interface IUserProfileRepository
{
    /// <summary>Devuelve el usuario con Profile y BusinessProfile (con seguimiento de cambios).</summary>
    Task<ApplicationUser?> GetUserWithProfilesAsync(Guid userId, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
