using Microsoft.EntityFrameworkCore;
using MotorLink.Domain.Entities;

namespace MotorLink.Application.Common;

/// <summary>
/// Acceso a datos para los casos de uso. Se expone como abstracción (implementada por AppDbContext en Infrastructure)
/// para no multiplicar repositorios triviales; Application solo conoce las abstracciones de EF Core, no el proveedor.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<ApplicationUser> Users { get; }
    DbSet<UserProfile> UserProfiles { get; }
    DbSet<BusinessProfile> BusinessProfiles { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<VehicleType> VehicleTypes { get; }
    DbSet<Brand> Brands { get; }
    DbSet<VehicleModel> Models { get; }
    DbSet<VehicleModelType> VehicleModelTypes { get; }
    DbSet<Vehicle> Vehicles { get; }
    DbSet<VehicleImage> VehicleImages { get; }
    DbSet<OrphanedImage> OrphanedImages { get; }
    DbSet<Listing> Listings { get; }
    DbSet<ListingOperation> ListingOperations { get; }
    DbSet<WorkRentCondition> WorkRentConditions { get; }
    DbSet<ListingStatusHistory> ListingStatusHistory { get; }
    DbSet<Department> Departments { get; }
    DbSet<Province> Provinces { get; }
    DbSet<District> Districts { get; }
    DbSet<Favorite> Favorites { get; }
    DbSet<Negotiation> Negotiations { get; }
    DbSet<NegotiationOffer> NegotiationOffers { get; }
    DbSet<Plan> Plans { get; }
    DbSet<Subscription> Subscriptions { get; }
    DbSet<Payment> Payments { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<ListingView> ListingViews { get; }
    DbSet<ContactEvent> ContactEvents { get; }
    DbSet<AuditLog> AuditLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// Ejecuta <paramref name="work"/> dentro de una transacción. Si se indica <paramref name="lockUserId"/> se toma un
    /// bloqueo exclusivo por usuario (sp_getapplock) que serializa las operaciones que consumen límites del plan.
    /// </summary>
    Task<T> ExecuteInTransactionAsync<T>(Guid? lockUserId, Func<Task<T>> work, CancellationToken ct);
}

public interface ICurrentUser
{
    Guid? UserId { get; }
    string? IpAddress { get; }
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public static class Paging
{
    public const int MaxPageSize = 50;

    public static (int Page, int PageSize) Normalize(int page, int pageSize, int max = MaxPageSize) =>
        (Math.Max(page, 1), Math.Clamp(pageSize <= 0 ? 20 : pageSize, 1, max));

    /// <summary>Paginación en SQL Server (COUNT + OFFSET/FETCH).</summary>
    public static async Task<PagedResult<T>> ToPagedAsync<T>(this IQueryable<T> query, int page, int pageSize, CancellationToken ct)
    {
        (page, pageSize) = Normalize(page, pageSize);
        var total = await query.CountAsync(ct);
        var items = total == 0
            ? []
            : await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<T>(items, page, pageSize, total);
    }
}

public interface IAuditService
{
    /// <summary>Agrega el registro al contexto; se persiste con el SaveChanges del caso de uso.</summary>
    void Log(string action, string? entityName = null, string? entityId = null, string? details = null);
}

public sealed class AuditService(IApplicationDbContext db, ICurrentUser user, TimeProvider clock) : IAuditService
{
    public void Log(string action, string? entityName = null, string? entityId = null, string? details = null) =>
        db.AuditLogs.Add(new AuditLog
        {
            UserId = user.UserId, Action = action, EntityName = entityName, EntityId = entityId,
            Details = details, IpAddress = user.IpAddress, CreatedAt = clock.GetUtcNow().UtcDateTime
        });
}
