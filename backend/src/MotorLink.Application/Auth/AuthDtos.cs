using System.ComponentModel.DataAnnotations;
using MotorLink.Domain.Common;

namespace MotorLink.Application.Auth;

public sealed record RegisterRequest : IValidatableObject
{
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; init; } = string.Empty;

    /// <summary>La política de complejidad se aplica además mediante ASP.NET Core Identity.</summary>
    [Required, StringLength(128, MinimumLength = 8)]
    public string Password { get; init; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 1)]
    public string FirstName { get; init; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 1)]
    public string LastName { get; init; } = string.Empty;

    [Phone, StringLength(20)]
    public string? PhoneNumber { get; init; }

    public ProfileType ProfileType { get; init; } = ProfileType.Personal;

    /// <summary>Obligatorio cuando ProfileType es BUSINESS.</summary>
    [StringLength(200)]
    public string? BusinessName { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ProfileType == ProfileType.Business && string.IsNullOrWhiteSpace(BusinessName))
            yield return new ValidationResult("El nombre del negocio es obligatorio para perfiles BUSINESS.", [nameof(BusinessName)]);
    }
}

public sealed record LoginRequest
{
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; init; } = string.Empty;

    [Required, StringLength(128)]
    public string Password { get; init; } = string.Empty;
}

public sealed record RefreshRequest
{
    [Required, StringLength(512)]
    public string RefreshToken { get; init; } = string.Empty;
}

public sealed record LogoutRequest
{
    [Required, StringLength(512)]
    public string RefreshToken { get; init; } = string.Empty;

    /// <summary>Si es true revoca todas las sesiones del usuario, no solo la actual.</summary>
    public bool AllSessions { get; init; }
}

public sealed record ChangePasswordRequest
{
    [Required, StringLength(128)]
    public string CurrentPassword { get; init; } = string.Empty;

    [Required, StringLength(128, MinimumLength = 8)]
    public string NewPassword { get; init; } = string.Empty;
}

public sealed record UserDto(Guid Id, string Email, string FullName, IReadOnlyList<string> Roles);

public sealed record AuthResponse(
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt,
    UserDto User);
