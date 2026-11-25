using System.ComponentModel.DataAnnotations;
using MotorLink.Application.Abstractions;
using MotorLink.Domain.Common;
using MotorLink.Domain.Entities;
using MotorLink.Domain.Exceptions;

namespace MotorLink.Application.Users;

public sealed record BusinessProfileDto(string BusinessName, string? Ruc, string? Description, string? Website,
    string? PhoneNumber, int? DistrictId, string? Address, bool IsVerified);

public sealed record ProfileDto(
    Guid UserId, string Email, ProfileType ProfileType, string FirstName, string LastName,
    string? PhoneNumber, string? DocumentNumber, int? DistrictId, string? Address,
    BusinessProfileDto? Business);

public sealed record UpdateProfileRequest : IValidatableObject
{
    [Required, StringLength(100, MinimumLength = 1)]
    public string FirstName { get; init; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 1)]
    public string LastName { get; init; } = string.Empty;

    [Phone, StringLength(20)]
    public string? PhoneNumber { get; init; }

    [StringLength(20)]
    public string? DocumentNumber { get; init; }

    public int? DistrictId { get; init; }

    [StringLength(300)]
    public string? Address { get; init; }

    public ProfileType ProfileType { get; init; } = ProfileType.Personal;

    // Datos del negocio (solo si ProfileType = BUSINESS)
    [StringLength(200)] public string? BusinessName { get; init; }
    [RegularExpression(@"^\d{11}$", ErrorMessage = "El RUC debe tener 11 dígitos.")] public string? Ruc { get; init; }
    [StringLength(1000)] public string? BusinessDescription { get; init; }
    [Url, StringLength(300)] public string? Website { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ProfileType == ProfileType.Business && string.IsNullOrWhiteSpace(BusinessName))
            yield return new ValidationResult("El nombre del negocio es obligatorio para perfiles BUSINESS.", [nameof(BusinessName)]);
    }
}

public interface IUserService
{
    Task<ProfileDto> GetProfileAsync(Guid userId, CancellationToken ct);
    Task<ProfileDto> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken ct);
}

public sealed class UserService(IUserProfileRepository repository) : IUserService
{
    public async Task<ProfileDto> GetProfileAsync(Guid userId, CancellationToken ct)
    {
        var user = await repository.GetUserWithProfilesAsync(userId, ct)
            ?? throw new NotFoundException("Usuario no encontrado.");
        return Map(user);
    }

    public async Task<ProfileDto> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken ct)
    {
        var user = await repository.GetUserWithProfilesAsync(userId, ct)
            ?? throw new NotFoundException("Usuario no encontrado.");

        var profile = user.Profile ??= new UserProfile { UserId = userId };
        profile.FirstName = request.FirstName.Trim();
        profile.LastName = request.LastName.Trim();
        profile.PhoneNumber = request.PhoneNumber;
        profile.DocumentNumber = request.DocumentNumber;
        profile.DistrictId = request.DistrictId;
        profile.Address = request.Address;
        profile.ProfileType = request.ProfileType;

        if (request.ProfileType == ProfileType.Business)
        {
            // Sin Id explícito: EF lo detecta como nuevo (Added) y genera la clave; con Id lo trataría como existente.
            var biz = user.BusinessProfile ??= new BusinessProfile { UserId = userId };
            biz.IsDeleted = false;
            biz.DeletedAt = null;
            biz.BusinessName = request.BusinessName!.Trim();
            biz.Ruc = request.Ruc;
            biz.Description = request.BusinessDescription;
            biz.Website = request.Website;
            biz.PhoneNumber = request.PhoneNumber;
            biz.DistrictId = request.DistrictId;
            biz.Address = request.Address;
        }

        // El repositorio traduce violaciones de unicidad/FK (RUC duplicado, distrito inexistente) a ConflictException.
        await repository.SaveChangesAsync(ct);
        return Map(user);
    }

    private static ProfileDto Map(ApplicationUser u)
    {
        var p = u.Profile;
        var b = u.BusinessProfile is { IsDeleted: false } biz
            ? new BusinessProfileDto(biz.BusinessName, biz.Ruc, biz.Description, biz.Website,
                biz.PhoneNumber, biz.DistrictId, biz.Address, biz.IsVerified)
            : null;
        return new ProfileDto(u.Id, u.Email ?? string.Empty, p?.ProfileType ?? ProfileType.Personal,
            p?.FirstName ?? string.Empty, p?.LastName ?? string.Empty, p?.PhoneNumber, p?.DocumentNumber,
            p?.DistrictId, p?.Address, b);
    }
}
