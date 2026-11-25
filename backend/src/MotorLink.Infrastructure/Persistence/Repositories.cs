using Microsoft.EntityFrameworkCore;
using MotorLink.Application.Abstractions;
using MotorLink.Domain.Common;
using MotorLink.Domain.Entities;
using MotorLink.Domain.Exceptions;

namespace MotorLink.Infrastructure.Persistence;

public sealed class RefreshTokenRepository(AppDbContext db) : IRefreshTokenRepository
{
    public void Add(RefreshToken token) => db.RefreshTokens.Add(token);

    public Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken ct) =>
        db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

    public async Task RevokeFamilyAsync(Guid familyId, RefreshTokenRevocationReason reason, DateTime utcNow, CancellationToken ct)
    {
        var active = await db.RefreshTokens.Where(t => t.FamilyId == familyId && t.RevokedAt == null).ToListAsync(ct);
        foreach (var t in active) t.Revoke(utcNow, reason);
    }

    public async Task RevokeAllForUserAsync(Guid userId, RefreshTokenRevocationReason reason, DateTime utcNow, CancellationToken ct)
    {
        var active = await db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAt == null).ToListAsync(ct);
        foreach (var t in active) t.Revoke(utcNow, reason);
    }

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}

public sealed class UserProfileRepository(AppDbContext db) : IUserProfileRepository
{
    public Task<ApplicationUser?> GetUserWithProfilesAsync(Guid userId, CancellationToken ct) =>
        db.Users.Include(u => u.Profile).Include(u => u.BusinessProfile)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

    public async Task SaveChangesAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        // 2601/2627: violación de índice/restricción única; 547: violación de clave foránea (independiente del idioma del servidor)
        catch (DbUpdateException ex) when (ex.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 or 547 })
        {
            throw new ConflictException("El RUC ya está registrado o el distrito indicado no existe.");
        }
    }
}
