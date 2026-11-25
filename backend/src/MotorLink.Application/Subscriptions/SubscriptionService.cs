using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using MotorLink.Application.Common;
using MotorLink.Application.Notifications;
using MotorLink.Domain.Common;
using MotorLink.Domain.Entities;
using MotorLink.Domain.Exceptions;
using MotorLink.Domain.Rules;

namespace MotorLink.Application.Subscriptions;

public sealed record SubscriptionDto(
    Guid Id, Guid UserId, string PlanCode, string PlanName, decimal MonthlyPrice, SubscriptionStatus Status,
    DateTime StartsAt, DateTime? EndsAt, bool AutoRenew, DateTime? CancelledAt, PaymentMode PaymentMode, DateTime CreatedAt);

public sealed record PaymentDto(
    Guid Id, Guid UserId, Guid? SubscriptionId, decimal Amount, Currency Currency, PaymentStatus Status,
    bool IsSimulated, string? Provider, string? ExternalReference, DateTime CreatedAt, DateTime? PaidAt);

public sealed record CurrentPlanDto(SubscriptionDto Subscription, PlanDto Plan, PlanUsageDto Usage);

public sealed record CheckoutRequest([Required, StringLength(30)] string PlanCode);
public sealed record AdminAssignRequest(
    [Required] Guid UserId, [Required, StringLength(30)] string PlanCode,
    [Range(1, 36)] int Months = 1);
public sealed record DemoPaymentRequest([Required] Guid SubscriptionId);

public sealed record PaymentRequest(Guid UserId, Guid SubscriptionId, decimal Amount, Currency Currency, string Description);
public sealed record PaymentResult(bool Success, bool IsSimulated, string Provider, string? ExternalReference, string? Error);

/// <summary>Contrato para pasarelas de pago (futura integración con Culqi). Esta versión solo trae un proveedor de demostración.</summary>
public interface IPaymentProvider
{
    string Name { get; }
    Task<PaymentResult> ChargeAsync(PaymentRequest request, CancellationToken ct);
}

public interface ISubscriptionService
{
    Task<CurrentPlanDto> GetCurrentAsync(Guid userId, CancellationToken ct);
    Task<PagedResult<SubscriptionDto>> HistoryAsync(Guid userId, int page, int pageSize, CancellationToken ct);
    Task<PagedResult<PaymentDto>> PaymentsAsync(Guid userId, int page, int pageSize, CancellationToken ct);
    /// <summary>Cambio de plan: crea una suscripción PENDING (el plan FREE equivale a cancelar la renovación).</summary>
    Task<SubscriptionDto> ChangePlanAsync(Guid userId, string planCode, CancellationToken ct);
    Task<SubscriptionDto> RenewAsync(Guid userId, CancellationToken ct);
    /// <summary>Paga una suscripción PENDING con el proveedor configurado (demostración) y la activa.</summary>
    Task<SubscriptionDto> PayAsync(Guid userId, Guid subscriptionId, CancellationToken ct);
    Task<SubscriptionDto> CancelAsync(Guid userId, Guid subscriptionId, CancellationToken ct);

    Task<PagedResult<SubscriptionDto>> AdminListAsync(SubscriptionStatus? status, Guid? userId, int page, int pageSize, CancellationToken ct);
    Task<PagedResult<PaymentDto>> AdminPaymentsAsync(bool? simulated, int page, int pageSize, CancellationToken ct);
    Task<SubscriptionDto> AdminAssignAsync(AdminAssignRequest r, CancellationToken ct);
    Task<SubscriptionDto> AdminActivateAsync(Guid subscriptionId, CancellationToken ct);
    Task<PaymentDto> AdminRegisterDemoPaymentAsync(Guid subscriptionId, CancellationToken ct);
    Task<SubscriptionDto> AdminCancelAsync(Guid subscriptionId, CancellationToken ct);
}

public sealed class SubscriptionService(
    IApplicationDbContext db, ISubscriptionPermissions permissions, IPlanLimitEnforcer enforcer, IPaymentProvider payments,
    INotificationService notifications, IAuditService audit, TimeProvider clock) : ISubscriptionService
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    // ---------------- usuario ----------------
    public async Task<CurrentPlanDto> GetCurrentAsync(Guid userId, CancellationToken ct)
    {
        var ent = await permissions.GetAsync(userId, ct);
        var sub = await db.Subscriptions.AsNoTracking().Include(s => s.Plan).FirstAsync(s => s.Id == ent.SubscriptionId, ct);
        var plan = await db.Plans.AsNoTracking().FirstAsync(p => p.Id == ent.PlanId, ct);
        return new CurrentPlanDto(ToDto(sub), PlanDto.From(plan), await permissions.GetUsageAsync(userId, ct));
    }

    public Task<PagedResult<SubscriptionDto>> HistoryAsync(Guid userId, int page, int pageSize, CancellationToken ct) =>
        ProjectSubs(db.Subscriptions.AsNoTracking().Where(s => s.UserId == userId).OrderByDescending(s => s.CreatedAt)).ToPagedAsync(page, pageSize, ct);

    public Task<PagedResult<PaymentDto>> PaymentsAsync(Guid userId, int page, int pageSize, CancellationToken ct) =>
        ProjectPayments(db.Payments.AsNoTracking().Where(p => p.UserId == userId).OrderByDescending(p => p.CreatedAt)).ToPagedAsync(page, pageSize, ct);

    public async Task<SubscriptionDto> ChangePlanAsync(Guid userId, string planCode, CancellationToken ct)
    {
        var plan = await db.Plans.FirstOrDefaultAsync(p => p.Code == planCode && p.IsActive, ct) ?? throw new NotFoundException("Plan no encontrado.");
        var current = await permissions.GetAsync(userId, ct);

        if (plan.Code == PlanCodes.Free)
        {
            // Bajar al plan gratuito: el plan de pago sigue vigente hasta su vencimiento y no se renueva.
            var paid = await db.Subscriptions.Include(s => s.Plan).FirstOrDefaultAsync(s => s.Id == current.SubscriptionId, ct);
            if (paid is null || paid.Plan.Code == PlanCodes.Free) throw new ConflictException("Ya estás en el plan gratuito.");
            return await CancelAsync(userId, paid.Id, ct);
        }
        if (plan.Id == current.PlanId) throw new ConflictException("Ya tienes este plan. Usa la renovación para extenderlo.");
        return await CreatePendingAsync(userId, plan, ct);
    }

    public async Task<SubscriptionDto> RenewAsync(Guid userId, CancellationToken ct)
    {
        var ent = await permissions.GetAsync(userId, ct);
        if (ent.PlanCode == PlanCodes.Free) throw new ConflictException("El plan gratuito no requiere renovación.");
        var plan = await db.Plans.FirstAsync(p => p.Id == ent.PlanId, ct);
        if (!plan.IsActive) throw new ConflictException("El plan actual ya no está disponible; elige otro.");
        return await CreatePendingAsync(userId, plan, ct);
    }

    public async Task<SubscriptionDto> PayAsync(Guid userId, Guid subscriptionId, CancellationToken ct)
    {
        var sub = await db.Subscriptions.Include(s => s.Plan).FirstOrDefaultAsync(s => s.Id == subscriptionId && s.UserId == userId, ct)
            ?? throw new NotFoundException("Suscripción no encontrada.");
        if (sub.Status != SubscriptionStatus.Pending) throw new ConflictException("Solo se pueden pagar suscripciones pendientes.");

        var payment = await db.Payments.FirstOrDefaultAsync(p => p.SubscriptionId == sub.Id && p.Status == PaymentStatus.Pending, ct);
        if (payment is null)
        {
            payment = NewPayment(sub, PaymentStatus.Pending, simulated: true, provider: payments.Name);
            db.Payments.Add(payment);
        }

        var result = await payments.ChargeAsync(new PaymentRequest(userId, sub.Id, sub.Plan.MonthlyPrice, Currency.Pen, $"Plan {sub.Plan.Name}"), ct);
        if (!result.Success)
        {
            payment.Status = PaymentStatus.Failed;
            await db.SaveChangesAsync(ct);
            throw new ConflictException(result.Error ?? "El pago no pudo procesarse.");
        }

        payment.Status = PaymentStatus.Paid; payment.PaidAt = Now; payment.IsSimulated = result.IsSimulated;
        payment.Provider = result.Provider; payment.ExternalReference = result.ExternalReference;
        return await ActivateAsync(sub, result.IsSimulated ? PaymentMode.Simulated : PaymentMode.Real, months: 1, "subscription.pay", ct);
    }

    public async Task<SubscriptionDto> CancelAsync(Guid userId, Guid subscriptionId, CancellationToken ct)
    {
        var sub = await db.Subscriptions.Include(s => s.Plan).FirstOrDefaultAsync(s => s.Id == subscriptionId && s.UserId == userId, ct)
            ?? throw new NotFoundException("Suscripción no encontrada.");
        await CancelCoreAsync(sub, ct);
        return ToDto(sub);
    }

    // ---------------- administración ----------------
    public Task<PagedResult<SubscriptionDto>> AdminListAsync(SubscriptionStatus? status, Guid? userId, int page, int pageSize, CancellationToken ct) =>
        ProjectSubs(db.Subscriptions.AsNoTracking().Where(s => (status == null || s.Status == status) && (userId == null || s.UserId == userId)).OrderByDescending(s => s.CreatedAt)).ToPagedAsync(page, pageSize, ct);

    public Task<PagedResult<PaymentDto>> AdminPaymentsAsync(bool? simulated, int page, int pageSize, CancellationToken ct) =>
        ProjectPayments(db.Payments.AsNoTracking().Where(p => simulated == null || p.IsSimulated == simulated).OrderByDescending(p => p.CreatedAt)).ToPagedAsync(page, pageSize, ct);

    public async Task<SubscriptionDto> AdminAssignAsync(AdminAssignRequest r, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(u => u.Id == r.UserId, ct)) throw new NotFoundException("Usuario no encontrado.");
        var plan = await db.Plans.FirstOrDefaultAsync(p => p.Code == r.PlanCode, ct) ?? throw new NotFoundException("Plan no encontrado.");
        var sub = new Subscription { Id = Guid.NewGuid(), UserId = r.UserId, PlanId = plan.Id, Plan = plan, Status = SubscriptionStatus.Pending, StartsAt = Now };
        db.Subscriptions.Add(sub);
        await db.SaveChangesAsync(ct);
        return await ActivateAsync(sub, PaymentMode.Complimentary, r.Months, "subscription.admin-assign", ct);
    }

    public async Task<SubscriptionDto> AdminActivateAsync(Guid subscriptionId, CancellationToken ct)
    {
        var sub = await db.Subscriptions.Include(s => s.Plan).FirstOrDefaultAsync(s => s.Id == subscriptionId, ct) ?? throw new NotFoundException("Suscripción no encontrada.");
        if (sub.Status != SubscriptionStatus.Pending) throw new ConflictException("Solo se pueden activar suscripciones pendientes.");
        return await ActivateAsync(sub, PaymentMode.Complimentary, 1, "subscription.admin-activate", ct);
    }

    public async Task<PaymentDto> AdminRegisterDemoPaymentAsync(Guid subscriptionId, CancellationToken ct)
    {
        var sub = await db.Subscriptions.Include(s => s.Plan).FirstOrDefaultAsync(s => s.Id == subscriptionId, ct) ?? throw new NotFoundException("Suscripción no encontrada.");
        if (sub.Status != SubscriptionStatus.Pending) throw new ConflictException("Solo se pueden pagar suscripciones pendientes.");
        var payment = NewPayment(sub, PaymentStatus.Paid, simulated: true, provider: "DEMO");
        payment.PaidAt = Now; payment.ExternalReference = "DEMO-" + Guid.NewGuid().ToString("N")[..12];
        db.Payments.Add(payment);
        await ActivateAsync(sub, PaymentMode.Simulated, 1, "payment.demo-register", ct);
        return new PaymentDto(payment.Id, payment.UserId, payment.SubscriptionId, payment.Amount, payment.Currency, payment.Status,
            payment.IsSimulated, payment.Provider, payment.ExternalReference, payment.CreatedAt, payment.PaidAt);
    }

    public async Task<SubscriptionDto> AdminCancelAsync(Guid subscriptionId, CancellationToken ct)
    {
        var sub = await db.Subscriptions.Include(s => s.Plan).FirstOrDefaultAsync(s => s.Id == subscriptionId, ct) ?? throw new NotFoundException("Suscripción no encontrada.");
        audit.Log("subscription.admin-cancel", nameof(Subscription), sub.Id.ToString());
        await CancelCoreAsync(sub, ct);
        return ToDto(sub);
    }

    // ---------------- núcleo ----------------
    private async Task<SubscriptionDto> CreatePendingAsync(Guid userId, Plan plan, CancellationToken ct)
    {
        // Solo una solicitud pendiente por usuario: las anteriores se cancelan.
        foreach (var old in await db.Subscriptions.Where(s => s.UserId == userId && s.Status == SubscriptionStatus.Pending).ToListAsync(ct))
        { old.Status = SubscriptionStatus.Cancelled; old.CancelledAt = Now; }

        var sub = new Subscription { Id = Guid.NewGuid(), UserId = userId, PlanId = plan.Id, Plan = plan, Status = SubscriptionStatus.Pending, StartsAt = Now, EndsAt = null };
        db.Subscriptions.Add(sub);
        db.Payments.Add(NewPayment(sub, PaymentStatus.Pending, simulated: true, provider: payments.Name));
        await db.SaveChangesAsync(ct);
        return ToDto(sub);
    }

    private Payment NewPayment(Subscription sub, PaymentStatus status, bool simulated, string provider) => new()
    {
        Id = Guid.NewGuid(), UserId = sub.UserId, SubscriptionId = sub.Id, Amount = sub.Plan.MonthlyPrice,
        Currency = Currency.Pen, Status = status, IsSimulated = simulated, Provider = provider
    };

    /// <summary>Activa la suscripción de forma atómica (bloqueo por usuario) y aplica los límites del nuevo plan.</summary>
    private Task<SubscriptionDto> ActivateAsync(Subscription sub, PaymentMode mode, int months, string auditAction, CancellationToken ct) =>
        db.ExecuteInTransactionAsync(sub.UserId, async () =>
        {
            var now = Now;
            // Si renueva el mismo plan vigente, el nuevo período se acumula al final del actual.
            var current = await db.Subscriptions.Where(s => s.UserId == sub.UserId && s.Status == SubscriptionStatus.Active && s.Id != sub.Id).ToListAsync(ct);
            var baseDate = current.Where(c => c.PlanId == sub.PlanId && c.EndsAt > now).Select(c => c.EndsAt).Max() ?? now;

            foreach (var c in current) { c.Status = SubscriptionStatus.Cancelled; c.CancelledAt = now; }
            await db.SaveChangesAsync(ct); // primero se libera la suscripción activa (índice único), luego se activa la nueva

            sub.Status = SubscriptionStatus.Active;
            sub.StartsAt = now;
            sub.EndsAt = sub.Plan.MonthlyPrice == 0 ? null : baseDate.AddMonths(months);
            sub.PaymentMode = mode;
            sub.CancelledAt = null;
            audit.Log(auditAction, nameof(Subscription), sub.Id.ToString(), $"{sub.Plan.Code} {mode}");
            notifications.Notify(sub.UserId, "SUBSCRIPTION_ACTIVATED", "Plan activado", $"Tu plan {sub.Plan.Name} está activo hasta {sub.EndsAt:yyyy-MM-dd}.", sub.Id);
            await db.SaveChangesAsync(ct);

            await enforcer.EnforceAsync(sub.UserId, ct); // por si un cambio administrativo reduce límites
            return ToDto(sub);
        }, ct);

    private async Task CancelCoreAsync(Subscription sub, CancellationToken ct)
    {
        switch (sub.Status)
        {
            case SubscriptionStatus.Pending:
                sub.Status = SubscriptionStatus.Cancelled; sub.CancelledAt = Now;
                foreach (var p in await db.Payments.Where(p => p.SubscriptionId == sub.Id && p.Status == PaymentStatus.Pending).ToListAsync(ct))
                    p.Status = PaymentStatus.Failed;
                break;
            case SubscriptionStatus.Active when sub.Plan.Code != PlanCodes.Free:
                // Se conserva el plan hasta su vencimiento, pero no se renueva.
                if (sub.CancelledAt is not null) throw new ConflictException("La suscripción ya fue cancelada.");
                sub.CancelledAt = Now; sub.AutoRenew = false;
                break;
            default:
                throw new ConflictException("La suscripción no puede cancelarse en su estado actual.");
        }
        await db.SaveChangesAsync(ct);
    }

    private static IQueryable<SubscriptionDto> ProjectSubs(IQueryable<Subscription> q) => q.Select(s => new SubscriptionDto(
        s.Id, s.UserId, s.Plan.Code, s.Plan.Name, s.Plan.MonthlyPrice, s.Status, s.StartsAt, s.EndsAt, s.AutoRenew, s.CancelledAt, s.PaymentMode, s.CreatedAt));

    private static IQueryable<PaymentDto> ProjectPayments(IQueryable<Payment> q) => q.Select(p => new PaymentDto(
        p.Id, p.UserId, p.SubscriptionId, p.Amount, p.Currency, p.Status, p.IsSimulated, p.Provider, p.ExternalReference, p.CreatedAt, p.PaidAt));

    private static SubscriptionDto ToDto(Subscription s) => new(s.Id, s.UserId, s.Plan.Code, s.Plan.Name, s.Plan.MonthlyPrice, s.Status,
        s.StartsAt, s.EndsAt, s.AutoRenew, s.CancelledAt, s.PaymentMode, s.CreatedAt);
}

/// <summary>Ajusta publicaciones y destacados a los límites del plan vigente, sin borrar vehículos ni fotografías.</summary>
public interface IPlanLimitEnforcer
{
    Task EnforceAsync(Guid userId, CancellationToken ct);
}

public sealed class PlanLimitEnforcer(
    IApplicationDbContext db, ISubscriptionPermissions permissions, INotificationService notifications, TimeProvider clock) : IPlanLimitEnforcer
{
    public async Task EnforceAsync(Guid userId, CancellationToken ct)
    {
        var ent = await permissions.GetAsync(userId, ct);
        var now = clock.GetUtcNow().UtcDateTime;
        var counting = ListingStateMachine.CountsTowardLimit;

        var listings = await db.Listings.Where(l => l.SellerId == userId && counting.Contains(l.Status))
            .OrderBy(l => l.PublishedAt ?? l.CreatedAt).ThenBy(l => l.CreatedAt).ToListAsync(ct);

        // Se conservan las más antiguas; el excedente se pausa (o vuelve a borrador si aún no se había aprobado).
        // Reservadas/Alquiladas no se tocan: hay un compromiso comercial en curso.
        var paused = 0;
        foreach (var l in listings.Skip(ent.MaxActiveListings).Where(l => l.Status is ListingStatus.Published or ListingStatus.PendingReview))
        {
            var from = l.Status;
            l.Status = from == ListingStatus.Published ? ListingStatus.Paused : ListingStatus.Draft;
            l.IsFeatured = false;
            db.ListingStatusHistory.Add(new ListingStatusHistory
            {
                ListingId = l.Id, FromStatus = from, ToStatus = l.Status, ChangedAt = now, Note = "Excede el límite del plan vigente"
            });
            paused++;
        }

        var featured = listings.Where(l => l.IsFeatured && l.Status != ListingStatus.Paused && l.Status != ListingStatus.Draft)
            .OrderBy(l => l.PublishedAt ?? l.CreatedAt).ToList();
        foreach (var l in featured.Skip(ent.MaxFeaturedListings)) l.IsFeatured = false;

        if (paused > 0)
            notifications.Notify(userId, "LISTINGS_PAUSED", "Publicaciones pausadas",
                $"{paused} publicación(es) se pausaron porque exceden el límite de tu plan {ent.PlanName}. Tus vehículos y fotografías se conservan.");
        await db.SaveChangesAsync(ct);
    }
}

/// <summary>Procesos periódicos de suscripciones (vencimientos).</summary>
public interface ISubscriptionMaintenance
{
    /// <summary>Marca como EXPIRED las suscripciones vencidas, asigna el plan gratuito y aplica sus límites. Devuelve cuántas venció.</summary>
    Task<int> ExpireDueAsync(CancellationToken ct);
}

public sealed class SubscriptionMaintenance(
    IApplicationDbContext db, ISubscriptionPermissions permissions, IPlanLimitEnforcer enforcer,
    INotificationService notifications, TimeProvider clock) : ISubscriptionMaintenance
{
    public async Task<int> ExpireDueAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var due = await db.Subscriptions.AsNoTracking()
            .Where(s => s.Status == SubscriptionStatus.Active && s.EndsAt != null && s.EndsAt <= now)
            .Select(s => new { s.Id, s.UserId }).Take(200).ToListAsync(ct);

        var expired = 0;
        foreach (var item in due)
        {
            await db.ExecuteInTransactionAsync<bool>(item.UserId, async () =>
            {
                var sub = await db.Subscriptions.Include(s => s.Plan).FirstOrDefaultAsync(s => s.Id == item.Id && s.Status == SubscriptionStatus.Active, ct);
                if (sub is null) return false;
                sub.Status = SubscriptionStatus.Expired;
                notifications.Notify(sub.UserId, "SUBSCRIPTION_EXPIRED", "Tu plan venció",
                    $"Tu plan {sub.Plan.Name} venció. Pasaste al plan gratuito; las publicaciones que excedan su límite se pausaron.", sub.Id);
                await db.SaveChangesAsync(ct);

                await permissions.EnsureFreeSubscriptionAsync(sub.UserId, ct);
                await enforcer.EnforceAsync(sub.UserId, ct);
                return true;
            }, ct);
            expired++;
        }
        return expired;
    }
}
