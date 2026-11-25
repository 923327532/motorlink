using Microsoft.EntityFrameworkCore;
using MotorLink.Application.Common;
using MotorLink.Domain.Common;
using MotorLink.Domain.Entities;
using MotorLink.Domain.Exceptions;
using MotorLink.Domain.Rules;

namespace MotorLink.Application.Subscriptions;

public enum PlanFeature { AdvancedStats, BusinessProfile, ExportReports, PrioritySupport }

/// <summary>Beneficios vigentes del usuario, resueltos desde la suscripción activa y su plan (datos en SQL Server).</summary>
public sealed record Entitlements(
    Guid SubscriptionId, int PlanId, string PlanCode, string PlanName, int MaxActiveListings, int MaxPhotosPerVehicle,
    int MaxFeaturedListings, bool HasAdvancedStats, bool HasBusinessProfile, bool CanExportReports, bool HasPrioritySupport,
    DateTime? EndsAt);

public sealed record PlanUsageDto(int ActiveListings, int MaxActiveListings, int FeaturedListings, int MaxFeaturedListings, int MaxPhotosPerVehicle);

/// <summary>
/// ÚNICO punto donde se evalúan beneficios y límites del plan. Ningún otro código compara nombres o códigos de plan.
/// Los métodos Ensure* deben invocarse dentro de una transacción con bloqueo por usuario para ser atómicos.
/// </summary>
public interface ISubscriptionPermissions
{
    Task<Entitlements> GetAsync(Guid userId, CancellationToken ct);
    Task<PlanUsageDto> GetUsageAsync(Guid userId, CancellationToken ct);
    Task EnsureCanActivateListingAsync(Guid userId, Guid? excludeListingId, CancellationToken ct);
    Task EnsureCanAddPhotoAsync(Guid userId, Guid vehicleId, CancellationToken ct);
    Task EnsureCanFeatureAsync(Guid userId, Guid listingId, CancellationToken ct);
    Task RequireFeatureAsync(Guid userId, PlanFeature feature, CancellationToken ct);
    Task EnsureFreeSubscriptionAsync(Guid userId, CancellationToken ct);
}

public sealed class SubscriptionPermissions(IApplicationDbContext db, TimeProvider clock) : ISubscriptionPermissions
{
    public async Task<Entitlements> GetAsync(Guid userId, CancellationToken ct)
    {
        var e = await Query(userId, ct);
        if (e is not null) return e;
        await EnsureFreeSubscriptionAsync(userId, ct);
        return await Query(userId, ct) ?? throw new InvalidOperationException("No existe el plan gratuito (FREE) en la base de datos.");
    }

    private async Task<Entitlements?> Query(Guid userId, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        return await db.Subscriptions.AsNoTracking()
            .Where(s => s.UserId == userId && s.Status == SubscriptionStatus.Active && (s.EndsAt == null || s.EndsAt > now))
            .OrderByDescending(s => s.Plan.MonthlyPrice).ThenByDescending(s => s.StartsAt)
            .Select(s => new Entitlements(s.Id, s.PlanId, s.Plan.Code, s.Plan.Name, s.Plan.MaxActiveListings, s.Plan.MaxPhotosPerVehicle,
                s.Plan.MaxFeaturedListings, s.Plan.HasAdvancedStats, s.Plan.HasBusinessProfile, s.Plan.CanExportReports,
                s.Plan.HasPrioritySupport, s.EndsAt))
            .FirstOrDefaultAsync(ct);
    }

    public async Task EnsureFreeSubscriptionAsync(Guid userId, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        if (await db.Subscriptions.AnyAsync(s => s.UserId == userId && s.Status == SubscriptionStatus.Active && (s.EndsAt == null || s.EndsAt > now), ct)) return;

        var free = await db.Plans.FirstOrDefaultAsync(p => p.Code == PlanCodes.Free, ct)
            ?? throw new InvalidOperationException("No existe el plan gratuito (FREE).");
        db.Subscriptions.Add(new Subscription
        {
            Id = Guid.NewGuid(), UserId = userId, PlanId = free.Id, StartsAt = now, EndsAt = null,
            Status = SubscriptionStatus.Active, PaymentMode = PaymentMode.None
        });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { /* otra solicitud creó la suscripción en paralelo (índice único de suscripción activa) */ }
    }

    public async Task<PlanUsageDto> GetUsageAsync(Guid userId, CancellationToken ct)
    {
        var ent = await GetAsync(userId, ct);
        return new PlanUsageDto(await CountActiveListings(userId, null, ct), ent.MaxActiveListings,
            await CountFeatured(userId, null, ct), ent.MaxFeaturedListings, ent.MaxPhotosPerVehicle);
    }

    public async Task EnsureCanActivateListingAsync(Guid userId, Guid? excludeListingId, CancellationToken ct)
    {
        var ent = await GetAsync(userId, ct);
        var used = await CountActiveListings(userId, excludeListingId, ct);
        if (used >= ent.MaxActiveListings)
            throw new PlanLimitException(
                $"Tu plan {ent.PlanName} permite {ent.MaxActiveListings} publicaciones activas y ya usas {used}. Mejora tu plan o pausa/archiva alguna publicación.",
                "MAX_ACTIVE_LISTINGS");
    }

    public async Task EnsureCanAddPhotoAsync(Guid userId, Guid vehicleId, CancellationToken ct)
    {
        var ent = await GetAsync(userId, ct);
        var count = await db.VehicleImages.CountAsync(i => i.VehicleId == vehicleId, ct);
        if (count >= ent.MaxPhotosPerVehicle)
            throw new PlanLimitException(
                $"Tu plan {ent.PlanName} permite {ent.MaxPhotosPerVehicle} fotografías por vehículo.", "MAX_PHOTOS_PER_VEHICLE");
    }

    public async Task EnsureCanFeatureAsync(Guid userId, Guid listingId, CancellationToken ct)
    {
        var ent = await GetAsync(userId, ct);
        var used = await CountFeatured(userId, listingId, ct);
        if (used >= ent.MaxFeaturedListings)
            throw new PlanLimitException(
                $"Tu plan {ent.PlanName} permite {ent.MaxFeaturedListings} publicaciones destacadas.", "MAX_FEATURED_LISTINGS");
    }

    public async Task RequireFeatureAsync(Guid userId, PlanFeature feature, CancellationToken ct)
    {
        var ent = await GetAsync(userId, ct);
        var (ok, name) = feature switch
        {
            PlanFeature.AdvancedStats => (ent.HasAdvancedStats, "estadísticas avanzadas"),
            PlanFeature.BusinessProfile => (ent.HasBusinessProfile, "perfil empresarial"),
            PlanFeature.ExportReports => (ent.CanExportReports, "exportación de reportes"),
            PlanFeature.PrioritySupport => (ent.HasPrioritySupport, "soporte prioritario"),
            _ => (false, feature.ToString())
        };
        if (!ok) throw new PlanLimitException($"Tu plan {ent.PlanName} no incluye {name}.", "FEATURE_" + feature.ToString().ToUpperInvariant());
    }

    private Task<int> CountActiveListings(Guid userId, Guid? exclude, CancellationToken ct)
    {
        var statuses = ListingStateMachine.CountsTowardLimit;
        return db.Listings.CountAsync(l => l.SellerId == userId && statuses.Contains(l.Status) && (exclude == null || l.Id != exclude), ct);
    }

    private Task<int> CountFeatured(Guid userId, Guid? exclude, CancellationToken ct)
    {
        var statuses = ListingStateMachine.CountsTowardLimit;
        return db.Listings.CountAsync(l => l.SellerId == userId && l.IsFeatured && statuses.Contains(l.Status) && (exclude == null || l.Id != exclude), ct);
    }
}
