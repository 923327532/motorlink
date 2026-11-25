using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using MotorLink.Application.Common;
using MotorLink.Domain.Common;
using MotorLink.Domain.Entities;
using MotorLink.Domain.Exceptions;
using MotorLink.Domain.Rules;

namespace MotorLink.Application.Admin;

public sealed record AdminUserDto(Guid Id, string Email, string FullName, IReadOnlyList<string> Roles, bool IsActive, DateTime CreatedAt, int Listings, string? PlanCode);
public sealed record SetUserActiveRequest(bool IsActive);
public sealed record AuditLogDto(long Id, Guid? UserId, string Action, string? EntityName, string? EntityId, string? Details, string? IpAddress, DateTime CreatedAt);

public sealed record PlanDistributionDto(string PlanCode, string PlanName, int ActiveSubscribers);
public sealed record CategoryCountDto(string Category, int Listings);
public sealed record MonthlyGrowthDto(string Month, int NewUsers, int NewListings);

/// <summary>
/// MRR (ingreso mensual recurrente) separado por origen:
///  • MrrReal: suscripciones activas pagadas con un proveedor real (hoy 0: aún no hay pasarela).
///  • MrrSimulated: suscripciones activas activadas con pagos de DEMOSTRACIÓN (no es dinero).
///  • ComplimentaryValue: valor de planes otorgados administrativamente sin pago (informativo).
/// </summary>
public sealed record AdminStatsDto(
    int UsersRegistered, int UsersActive, int PayingSubscribers, IReadOnlyList<PlanDistributionDto> PlanDistribution,
    IReadOnlyList<CategoryCountDto> ListingsByCategory, int VehiclesSold, int VehiclesRented,
    IReadOnlyList<MonthlyGrowthDto> MonthlyGrowth, decimal MrrReal, decimal MrrSimulated, decimal ComplimentaryValue);

public interface IAdminService
{
    Task<PagedResult<AdminUserDto>> ListUsersAsync(string? search, bool? active, int page, int pageSize, CancellationToken ct);
    Task<AdminUserDto> GetUserAsync(Guid id, CancellationToken ct);
    Task<AdminUserDto> SetUserActiveAsync(Guid adminId, Guid id, bool active, CancellationToken ct);
    Task<PagedResult<AuditLogDto>> ListAuditAsync(string? action, Guid? userId, DateTime? from, DateTime? to, int page, int pageSize, CancellationToken ct);
    Task<AdminStatsDto> GetStatsAsync(CancellationToken ct);
    Task<string> ExportUsersCsvAsync(CancellationToken ct);
    Task<string> ExportSubscriptionsCsvAsync(CancellationToken ct);
}

public sealed class AdminService(IApplicationDbContext db, IAuditService audit, TimeProvider clock) : IAdminService
{
    public Task<PagedResult<AdminUserDto>> ListUsersAsync(string? search, bool? active, int page, int pageSize, CancellationToken ct) =>
        ProjectUsers(db.Users.AsNoTracking()
            .Where(u => active == null || u.IsActive == active)
            .Where(u => string.IsNullOrWhiteSpace(search) || u.Email!.Contains(search!)
                        || (u.Profile != null && (u.Profile.FirstName + " " + u.Profile.LastName).Contains(search!)))
            .OrderByDescending(u => u.CreatedAt)).ToPagedAsync(page, pageSize, ct);

    public async Task<AdminUserDto> GetUserAsync(Guid id, CancellationToken ct) =>
        await ProjectUsers(db.Users.AsNoTracking().Where(u => u.Id == id)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException("Usuario no encontrado.");

    public async Task<AdminUserDto> SetUserActiveAsync(Guid adminId, Guid id, bool active, CancellationToken ct)
    {
        if (id == adminId && !active) throw new ConflictException("No puedes bloquear tu propia cuenta.");
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct) ?? throw new NotFoundException("Usuario no encontrado.");
        var now = clock.GetUtcNow().UtcDateTime;

        user.IsActive = active;
        if (!active)
        {
            // Se cierran todas las sesiones: el login y el refresh ya rechazan cuentas inactivas.
            foreach (var t in await db.RefreshTokens.Where(t => t.UserId == id && t.RevokedAt == null).ToListAsync(ct))
                t.Revoke(now, RefreshTokenRevocationReason.Admin);
        }
        else { user.LockoutEnd = null; user.AccessFailedCount = 0; }

        audit.Log(active ? "user.activate" : "user.block", nameof(ApplicationUser), id.ToString());
        await db.SaveChangesAsync(ct);
        return await GetUserAsync(id, ct);
    }

    public Task<PagedResult<AuditLogDto>> ListAuditAsync(string? action, Guid? userId, DateTime? from, DateTime? to, int page, int pageSize, CancellationToken ct) =>
        db.AuditLogs.AsNoTracking()
            .Where(a => (action == null || a.Action.StartsWith(action)) && (userId == null || a.UserId == userId)
                        && (from == null || a.CreatedAt >= from) && (to == null || a.CreatedAt <= to))
            .OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id)
            .Select(a => new AuditLogDto(a.Id, a.UserId, a.Action, a.EntityName, a.EntityId, a.Details, a.IpAddress, a.CreatedAt))
            .ToPagedAsync(page, pageSize, ct);

    public async Task<AdminStatsDto> GetStatsAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var registered = await db.Users.CountAsync(ct);
        var activeUsers = await db.Users.CountAsync(u => u.IsActive, ct);

        var activeSubs = db.Subscriptions.AsNoTracking().Where(s => s.Status == SubscriptionStatus.Active && (s.EndsAt == null || s.EndsAt > now));
        var paying = await activeSubs.Where(s => s.Plan.MonthlyPrice > 0).Select(s => s.UserId).Distinct().CountAsync(ct);
        var dist = await activeSubs.GroupBy(s => new { s.Plan.Code, s.Plan.Name, s.Plan.SortOrder })
            .Select(g => new PlanDistributionDto(g.Key.Code, g.Key.Name, g.Count())).ToListAsync(ct);

        var visible = ListingStateMachine.PublicVisible;
        var categoryRows = await db.Listings.AsNoTracking().Where(l => visible.Contains(l.Status))
            .GroupBy(l => l.Vehicle.VehicleType.Name).Select(g => new { Name = g.Key, N = g.Count() }).ToListAsync(ct);
        var byCategory = categoryRows.OrderByDescending(c => c.N).Select(c => new CategoryCountDto(c.Name, c.N)).ToList();

        var sold = await db.ListingStatusHistory.CountAsync(h => h.ToStatus == ListingStatus.Sold, ct);
        var rented = await db.ListingStatusHistory.CountAsync(h => h.ToStatus == ListingStatus.Rented, ct);

        var since = new DateTime(now.Year, now.Month, 1).AddMonths(-11);
        var users = await db.Users.Where(u => u.CreatedAt >= since).GroupBy(u => new { u.CreatedAt.Year, u.CreatedAt.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, N = g.Count() }).ToListAsync(ct);
        var listings = await db.Listings.Where(l => l.CreatedAt >= since).GroupBy(l => new { l.CreatedAt.Year, l.CreatedAt.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, N = g.Count() }).ToListAsync(ct);
        var growth = Enumerable.Range(0, 12).Select(i => since.AddMonths(i)).Select(m => new MonthlyGrowthDto($"{m:yyyy-MM}",
            users.Where(x => x.Year == m.Year && x.Month == m.Month).Sum(x => x.N),
            listings.Where(x => x.Year == m.Year && x.Month == m.Month).Sum(x => x.N))).ToList();

        decimal Mrr(PaymentMode mode) => activeSubs.Where(s => s.PaymentMode == mode).Sum(s => (decimal?)s.Plan.MonthlyPrice) ?? 0m;
        return new AdminStatsDto(registered, activeUsers, paying, dist.OrderBy(d => d.PlanCode).ToList(), byCategory, sold, rented, growth,
            Mrr(PaymentMode.Real), Mrr(PaymentMode.Simulated), Mrr(PaymentMode.Complimentary));
    }

    public async Task<string> ExportUsersCsvAsync(CancellationToken ct)
    {
        var rows = await ProjectUsers(db.Users.AsNoTracking().OrderBy(u => u.CreatedAt)).ToListAsync(ct);
        var sb = new StringBuilder("id,email,nombre,roles,activo,creado,publicaciones,plan\n");
        foreach (var u in rows)
            sb.AppendLine(string.Join(',', u.Id, Csv(u.Email), Csv(u.FullName), Csv(string.Join('|', u.Roles)), u.IsActive,
                u.CreatedAt.ToString("O", CultureInfo.InvariantCulture), u.Listings, u.PlanCode));
        return sb.ToString();
    }

    public async Task<string> ExportSubscriptionsCsvAsync(CancellationToken ct)
    {
        var rows = await db.Subscriptions.AsNoTracking().OrderBy(s => s.CreatedAt).Select(s => new
        { s.Id, s.UserId, s.Plan.Code, s.Plan.MonthlyPrice, s.Status, s.PaymentMode, s.StartsAt, s.EndsAt }).ToListAsync(ct);
        var sb = new StringBuilder("id,usuario,plan,precio_mensual,estado,modo_pago,inicio,vencimiento\n");
        foreach (var r in rows)
            sb.AppendLine(string.Join(',', r.Id, r.UserId, r.Code, r.MonthlyPrice.ToString(CultureInfo.InvariantCulture), r.Status, r.PaymentMode,
                r.StartsAt.ToString("O", CultureInfo.InvariantCulture), r.EndsAt?.ToString("O", CultureInfo.InvariantCulture)));
        return sb.ToString();
    }

    private IQueryable<AdminUserDto> ProjectUsers(IQueryable<ApplicationUser> q) => q.Select(u => new AdminUserDto(
        u.Id, u.Email!, u.Profile != null ? u.Profile.FirstName + " " + u.Profile.LastName : u.Email!,
        u.UserRoles.Select(r => r.Role.Name!).ToList(), u.IsActive, u.CreatedAt,
        db.Listings.Count(l => l.SellerId == u.Id),
        db.Subscriptions.Where(s => s.UserId == u.Id && s.Status == SubscriptionStatus.Active).OrderByDescending(s => s.Plan.MonthlyPrice).Select(s => s.Plan.Code).FirstOrDefault()));

    /// <summary>Escapa un campo CSV y neutraliza inyección de fórmulas (=, +, -, @) al abrir en hojas de cálculo.</summary>
    internal static string Csv(string? v)
    {
        if (string.IsNullOrEmpty(v)) return "";
        if ("=+-@".Contains(v[0])) v = "'" + v;
        return v.Contains(',') || v.Contains('"') || v.Contains('\n') ? $"\"{v.Replace("\"", "\"\"")}\"" : v;
    }
}
