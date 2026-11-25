using System.Linq.Expressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using MotorLink.Application.Common;
using MotorLink.Domain.Common;
using MotorLink.Domain.Entities;

namespace MotorLink.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options, TimeProvider clock)
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid, IdentityUserClaim<Guid>, ApplicationUserRole,
        IdentityUserLogin<Guid>, IdentityRoleClaim<Guid>, IdentityUserToken<Guid>>(options), IApplicationDbContext
{
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<BusinessProfile> BusinessProfiles => Set<BusinessProfile>();
    public DbSet<VehicleType> VehicleTypes => Set<VehicleType>();
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<VehicleModel> Models => Set<VehicleModel>();
    public DbSet<VehicleModelType> VehicleModelTypes => Set<VehicleModelType>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<VehicleImage> VehicleImages => Set<VehicleImage>();
    public DbSet<OrphanedImage> OrphanedImages => Set<OrphanedImage>();
    public DbSet<Listing> Listings => Set<Listing>();
    public DbSet<ListingOperation> ListingOperations => Set<ListingOperation>();
    public DbSet<WorkRentCondition> WorkRentConditions => Set<WorkRentCondition>();
    public DbSet<ListingStatusHistory> ListingStatusHistory => Set<ListingStatusHistory>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Province> Provinces => Set<Province>();
    public DbSet<District> Districts => Set<District>();
    public DbSet<Favorite> Favorites => Set<Favorite>();
    public DbSet<Negotiation> Negotiations => Set<Negotiation>();
    public DbSet<NegotiationOffer> NegotiationOffers => Set<NegotiationOffer>();
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<ListingView> ListingViews => Set<ListingView>();
    public DbSet<ContactEvent> ContactEvents => Set<ContactEvent>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Precios y tarifas: DECIMAL(18,2). Fechas: siempre UTC.
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<NullableUtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        ApplySoftDeleteFilters(builder);
    }

    /// <summary>Filtro global: las entidades eliminadas lógicamente no aparecen en consultas.</summary>
    private static void ApplySoftDeleteFilters(ModelBuilder builder)
    {
        foreach (var entityType in builder.Model.GetEntityTypes()
                     .Where(t => typeof(ISoftDelete).IsAssignableFrom(t.ClrType) && t.BaseType is null))
        {
            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var isDeleted = Expression.Property(parameter, nameof(ISoftDelete.IsDeleted));
            var filter = Expression.Lambda(Expression.Not(isDeleted), parameter);
            entityType.SetQueryFilter(filter);
        }
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditRules();
        return base.SaveChangesAsync(cancellationToken);
    }

    public async Task<T> ExecuteInTransactionAsync<T>(Guid? lockUserId, Func<Task<T>> work, CancellationToken ct)
    {
        if (Database.CurrentTransaction is not null) // ya estamos dentro de una transacción (anidamiento)
        {
            if (lockUserId.HasValue) await AcquireUserLockAsync(lockUserId.Value, ct);
            return await work();
        }

        // Con EnableRetryOnFailure toda transacción manual debe ejecutarse dentro de la estrategia de ejecución:
        // si hay un fallo transitorio se repite el bloque completo (transacción, bloqueo y trabajo).
        var strategy = Database.CreateExecutionStrategy();
        var attempt = 0;
        return await strategy.ExecuteAsync(async () =>
        {
            if (attempt++ > 0) ChangeTracker.Clear();   // descarta cambios a medias del intento fallido
            await using var tx = await Database.BeginTransactionAsync(ct);
            if (lockUserId.HasValue) await AcquireUserLockAsync(lockUserId.Value, ct);
            var result = await work();   // si lanza, la transacción se revierte al liberar 'tx'
            await tx.CommitAsync(ct);
            return result;
        });
    }

    /// <summary>
    /// Bloqueo de aplicación exclusivo por usuario (liberado al terminar la transacción). Serializa las operaciones que
    /// consumen límites del plan para que solicitudes simultáneas no superen el cupo contratado.
    /// </summary>
    private async Task AcquireUserLockAsync(Guid userId, CancellationToken ct)
    {
        var resource = "motorlink:user:" + userId.ToString("N");
        var rows = await Database.SqlQuery<int>(
            $"DECLARE @r int; EXEC @r = sp_getapplock @Resource = {resource}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000; SELECT @r AS [Value]")
            .ToListAsync(ct);
        if (rows.Count == 0 || rows[0] < 0)
            throw new MotorLink.Domain.Exceptions.ConflictException("Tu cuenta está procesando otra operación. Inténtalo nuevamente en unos segundos.");
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyAuditRules();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    private void ApplyAuditRules()
    {
        var now = clock.GetUtcNow().UtcDateTime;
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.Entity is IAuditable auditable)
            {
                if (entry.State == EntityState.Added && auditable.CreatedAt == default) auditable.CreatedAt = now;
                else if (entry.State == EntityState.Modified) auditable.UpdatedAt = now;
            }

            // Borrado físico -> borrado lógico
            if (entry.State == EntityState.Deleted && entry.Entity is ISoftDelete soft)
            {
                entry.State = EntityState.Modified;
                soft.IsDeleted = true;
                soft.DeletedAt = now;
                if (entry.Entity is IAuditable a) a.UpdatedAt = now;
            }
        }
    }
}

public sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    v => v.Kind == DateTimeKind.Local ? v.ToUniversalTime() : v,
    v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

public sealed class NullableUtcDateTimeConverter() : ValueConverter<DateTime?, DateTime?>(
    v => v.HasValue && v.Value.Kind == DateTimeKind.Local ? v.Value.ToUniversalTime() : v,
    v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);
