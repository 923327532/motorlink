using Microsoft.AspNetCore.Identity;
using MotorLink.Domain.Common;

namespace MotorLink.Domain.Entities;

public static class RoleNames
{
    public const string Admin = "ADMIN";
    public const string User = "USER";
}

/// <summary>Tabla Users (ASP.NET Core Identity adaptado, clave Guid).</summary>
public class ApplicationUser : IdentityUser<Guid>, IAuditable, ISoftDelete
{
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public UserProfile? Profile { get; set; }
    public BusinessProfile? BusinessProfile { get; set; }
    public ICollection<ApplicationUserRole> UserRoles { get; set; } = new List<ApplicationUserRole>();
}

/// <summary>Tabla Roles (ADMIN, USER).</summary>
public class ApplicationRole : IdentityRole<Guid>
{
    public ApplicationRole() { }
    public ApplicationRole(string name) : base(name) { }
}

/// <summary>Tabla UserRoles.</summary>
public class ApplicationUserRole : IdentityUserRole<Guid>
{
    public ApplicationUser User { get; set; } = null!;
    public ApplicationRole Role { get; set; } = null!;
}

public class UserProfile : IAuditable
{
    /// <summary>PK y FK a Users (relación 1:1).</summary>
    public Guid UserId { get; set; }
    public ProfileType ProfileType { get; set; } = ProfileType.Personal;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? DocumentNumber { get; set; }
    public int? DistrictId { get; set; }
    public string? Address { get; set; }
    public string? AvatarUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public ApplicationUser User { get; set; } = null!;
    public District? District { get; set; }
}

public class BusinessProfile : SoftDeletableEntity<Guid>
{
    public Guid UserId { get; set; }
    public string BusinessName { get; set; } = string.Empty;
    public string? Ruc { get; set; }
    public string? Description { get; set; }
    public string? Website { get; set; }
    public string? PhoneNumber { get; set; }
    public int? DistrictId { get; set; }
    public string? Address { get; set; }
    public bool IsVerified { get; set; }

    public ApplicationUser User { get; set; } = null!;
    public District? District { get; set; }
}

public class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    /// <summary>Identifica la cadena de rotación; se usa para revocar toda la sesión ante reutilización.</summary>
    public Guid FamilyId { get; set; }
    /// <summary>SHA-256 (hex) del token. El token en claro nunca se almacena.</summary>
    public string TokenHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string? CreatedByIp { get; set; }
    public DateTime? RevokedAt { get; set; }
    public RefreshTokenRevocationReason? RevocationReason { get; set; }
    public string? ReplacedByTokenHash { get; set; }

    public ApplicationUser User { get; set; } = null!;

    public bool IsRevoked => RevokedAt.HasValue;
    public bool IsExpired(DateTime utcNow) => utcNow >= ExpiresAt;

    public void Revoke(DateTime utcNow, RefreshTokenRevocationReason reason, string? replacedByHash = null)
    {
        if (IsRevoked) return;
        RevokedAt = utcNow;
        RevocationReason = reason;
        ReplacedByTokenHash = replacedByHash;
    }
}
