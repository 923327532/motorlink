using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MotorLink.Application.Abstractions;
using MotorLink.Application.Auth;
using MotorLink.Domain.Common;
using MotorLink.Domain.Entities;
using MotorLink.Domain.Exceptions;
using MotorLink.Infrastructure.Persistence;

namespace MotorLink.Infrastructure.Identity;

public sealed class IdentityService(UserManager<ApplicationUser> userManager, AppDbContext db) : IIdentityService
{
    public async Task<UserAccount> CreateUserAsync(RegisterRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim();
        if (await userManager.FindByEmailAsync(email) is not null)
            throw new ConflictException("El correo electrónico ya está registrado.");

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            PhoneNumber = request.PhoneNumber,
            IsActive = true,
            Profile = new UserProfile
            {
                ProfileType = request.ProfileType,
                FirstName = request.FirstName.Trim(),
                LastName = request.LastName.Trim(),
                PhoneNumber = request.PhoneNumber
            }
        };
        user.Profile.UserId = user.Id;

        if (request.ProfileType == ProfileType.Business)
        {
            user.BusinessProfile = new BusinessProfile
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                BusinessName = request.BusinessName!.Trim(),
                PhoneNumber = request.PhoneNumber
            };
        }

        // Usuario + perfil + rol en una única transacción.
        // Dentro de la estrategia de ejecución (reintentos de Azure SQL): si el intento falla por un error transitorio se repite completo.
        var attempt = 0;
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            if (attempt++ > 0) db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var created = await userManager.CreateAsync(user, request.Password);
            if (!created.Succeeded) ThrowIdentityErrors(created, "Password");

            var role = await userManager.AddToRoleAsync(user, RoleNames.User);
            if (!role.Succeeded) ThrowIdentityErrors(role, "Role");
            await tx.CommitAsync(ct);
        });

        return (await FindByIdAsync(user.Id, ct))!;
    }

    public Task<UserAccount?> FindByEmailAsync(string email, CancellationToken ct)
    {
        var normalized = userManager.NormalizeEmail(email.Trim());
        return QueryAccounts().Where(x => x.NormalizedEmail == normalized).Select(Project).FirstOrDefaultAsync(ct);
    }

    public Task<UserAccount?> FindByIdAsync(Guid userId, CancellationToken ct) =>
        QueryAccounts().Where(x => x.Id == userId).Select(Project).FirstOrDefaultAsync(ct);

    public async Task<PasswordCheckResult> CheckPasswordAsync(Guid userId, string password, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null) return PasswordCheckResult.Failed;
        if (await userManager.IsLockedOutAsync(user)) return PasswordCheckResult.LockedOut;

        if (await userManager.CheckPasswordAsync(user, password))
        {
            await userManager.ResetAccessFailedCountAsync(user);
            return PasswordCheckResult.Success;
        }

        await userManager.AccessFailedAsync(user); // incrementa el contador y bloquea al llegar al umbral
        return await userManager.IsLockedOutAsync(user) ? PasswordCheckResult.LockedOut : PasswordCheckResult.Failed;
    }

    public async Task ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new NotFoundException("Usuario no encontrado.");

        var result = await userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        if (result.Succeeded) return;

        var mismatch = result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.PasswordMismatch));
        if (mismatch)
            throw new BusinessValidationException(new Dictionary<string, string[]>
            {
                ["CurrentPassword"] = ["La contraseña actual es incorrecta."]
            });
        ThrowIdentityErrors(result, "NewPassword");
    }

    public async Task<IReadOnlyList<UserAccount>> ListUsersAsync(int page, int pageSize, CancellationToken ct) =>
        await QueryAccounts().OrderBy(x => x.CreatedAt).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).Select(Project).ToListAsync(ct);

    // ---- helpers ----
    private IQueryable<ApplicationUser> QueryAccounts() => db.Users.AsNoTracking();

    private static readonly System.Linq.Expressions.Expression<Func<ApplicationUser, UserAccount>> Project = u =>
        new UserAccount(
            u.Id,
            u.Email!,
            u.Profile != null ? u.Profile.FirstName + " " + u.Profile.LastName : u.Email!,
            u.IsActive,
            u.UserRoles.Select(r => r.Role.Name!).ToList());

    private static void ThrowIdentityErrors(IdentityResult result, string defaultField)
    {
        var errors = result.Errors
            .GroupBy(e => e.Code.StartsWith("Password", StringComparison.Ordinal) ? "Password" : defaultField)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());
        throw new BusinessValidationException(errors);
    }
}
