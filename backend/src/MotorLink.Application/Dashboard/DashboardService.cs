using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using MotorLink.Application.Common;
using MotorLink.Application.Subscriptions;
using MotorLink.Domain.Common;
using MotorLink.Domain.Rules;

namespace MotorLink.Application.Dashboard;

public sealed record MonthlyPointDto(string Month, int Views, int ContactClicks, int ProposalsReceived, int VehiclesSold, int VehiclesRented);

/// <summary>
/// Indicadores del usuario. Fórmulas:
///  • ActiveListings / VehiclesRegistered / PlanUsage: estado ACTUAL (no dependen del rango de fechas).
///  • Views: visualizaciones de mis publicaciones, excluyendo las del propio anunciante, con ViewedAt en el rango.
///  • ContactClicks: clics de contacto registrados (intenciones; NO son contactos concretados).
///  • ProposalsReceived / ProposalsSent: negociaciones iniciadas en el rango donde soy vendedor / comprador.
///  • AcceptedNegotiations: negociaciones donde participo, aceptadas, cuya última actualización cae en el rango (acuerdos de precio).
///  • VehiclesSold / VehiclesRented: cambios de estado a SOLD / RENTED registrados en el historial dentro del rango.
///  • ConversionRate = aceptadas entre las propuestas recibidas en el rango / ProposalsReceived (0 si no hay propuestas).
///  • ContactRate = ContactClicks / Views (0 si no hay visualizaciones).
/// </summary>
public sealed record DashboardDto(
    DateTime From, DateTime To, string PlanCode, PlanUsageDto PlanUsage, int ActiveListings, int VehiclesRegistered,
    int Views, int ContactClicks, int ProposalsReceived, int ProposalsSent, int AcceptedNegotiations,
    int VehiclesSold, int VehiclesRented, double ConversionRate, double ContactRate,
    bool AdvancedStats, IReadOnlyList<MonthlyPointDto>? MonthlyEvolution);

public interface IDashboardService
{
    Task<DashboardDto> GetAsync(Guid userId, DateTime? from, DateTime? to, CancellationToken ct);
    /// <summary>Exportación CSV; requiere un plan con exportación de reportes.</summary>
    Task<string> ExportCsvAsync(Guid userId, DateTime? from, DateTime? to, CancellationToken ct);
}

public sealed class DashboardService(IApplicationDbContext db, ISubscriptionPermissions permissions, TimeProvider clock) : IDashboardService
{
    public async Task<DashboardDto> GetAsync(Guid userId, DateTime? from, DateTime? to, CancellationToken ct)
    {
        var end = (to ?? clock.GetUtcNow().UtcDateTime).ToUniversalTime();
        var start = (from ?? end.AddDays(-30)).ToUniversalTime();
        if (start > end) throw new Domain.Exceptions.BusinessValidationException(new Dictionary<string, string[]> { ["from"] = ["'from' debe ser anterior a 'to'."] });
        if ((end - start).TotalDays > 366) throw new Domain.Exceptions.BusinessValidationException(new Dictionary<string, string[]> { ["from"] = ["El rango máximo es de 366 días."] });

        var ent = await permissions.GetAsync(userId, ct);
        var usage = await permissions.GetUsageAsync(userId, ct);

        var activeStatuses = ListingStateMachine.CountsTowardLimit;
        var activeListings = await db.Listings.CountAsync(l => l.SellerId == userId && activeStatuses.Contains(l.Status), ct);
        var vehicles = await db.Vehicles.CountAsync(v => v.OwnerId == userId, ct);

        var views = await db.ListingViews.CountAsync(v => v.Listing.SellerId == userId && v.ViewedAt >= start && v.ViewedAt <= end, ct);
        var clicks = await db.ContactEvents.CountAsync(c => c.Listing.SellerId == userId && c.CreatedAt >= start && c.CreatedAt <= end, ct);
        var received = await db.Negotiations.CountAsync(n => n.SellerId == userId && n.CreatedAt >= start && n.CreatedAt <= end, ct);
        var sent = await db.Negotiations.CountAsync(n => n.BuyerId == userId && n.CreatedAt >= start && n.CreatedAt <= end, ct);
        var acceptedReceived = await db.Negotiations.CountAsync(n => n.SellerId == userId && n.Status == NegotiationStatus.Accepted && n.CreatedAt >= start && n.CreatedAt <= end, ct);
        var accepted = await db.Negotiations.CountAsync(n => (n.SellerId == userId || n.BuyerId == userId) && n.Status == NegotiationStatus.Accepted
                                                              && (n.UpdatedAt ?? n.CreatedAt) >= start && (n.UpdatedAt ?? n.CreatedAt) <= end, ct);
        var sold = await db.ListingStatusHistory.CountAsync(h => h.ToStatus == ListingStatus.Sold && h.ChangedAt >= start && h.ChangedAt <= end
                                                                 && db.Listings.IgnoreQueryFilters().Any(l => l.Id == h.ListingId && l.SellerId == userId), ct);
        var rented = await db.ListingStatusHistory.CountAsync(h => h.ToStatus == ListingStatus.Rented && h.ChangedAt >= start && h.ChangedAt <= end
                                                                   && db.Listings.IgnoreQueryFilters().Any(l => l.Id == h.ListingId && l.SellerId == userId), ct);

        IReadOnlyList<MonthlyPointDto>? monthly = null;
        if (ent.HasAdvancedStats) monthly = await MonthlyAsync(userId, start, end, ct);

        return new DashboardDto(start, end, ent.PlanCode, usage, activeListings, vehicles, views, clicks, received, sent, accepted, sold, rented,
            SafeRate(acceptedReceived, received), SafeRate(clicks, views), ent.HasAdvancedStats, monthly);
    }

    public async Task<string> ExportCsvAsync(Guid userId, DateTime? from, DateTime? to, CancellationToken ct)
    {
        await permissions.RequireFeatureAsync(userId, PlanFeature.ExportReports, ct);
        var d = await GetAsync(userId, from, to, ct);
        var inv = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        sb.AppendLine("indicador,valor");
        void Row(string k, object v) => sb.AppendLine($"{k},{Convert.ToString(v, inv)}");
        Row("desde", d.From.ToString("yyyy-MM-dd")); Row("hasta", d.To.ToString("yyyy-MM-dd"));
        Row("publicaciones_activas", d.ActiveListings); Row("vehiculos_registrados", d.VehiclesRegistered);
        Row("visualizaciones", d.Views); Row("clics_contacto", d.ContactClicks);
        Row("propuestas_recibidas", d.ProposalsReceived); Row("propuestas_enviadas", d.ProposalsSent);
        Row("negociaciones_aceptadas", d.AcceptedNegotiations); Row("vehiculos_vendidos", d.VehiclesSold); Row("vehiculos_alquilados", d.VehiclesRented);
        Row("tasa_conversion", d.ConversionRate); Row("tasa_contacto", d.ContactRate);
        if (d.MonthlyEvolution is not null)
        {
            sb.AppendLine().AppendLine("mes,visualizaciones,clics_contacto,propuestas_recibidas,vendidos,alquilados");
            foreach (var m in d.MonthlyEvolution) sb.AppendLine($"{m.Month},{m.Views},{m.ContactClicks},{m.ProposalsReceived},{m.VehiclesSold},{m.VehiclesRented}");
        }
        return sb.ToString();
    }

    private async Task<IReadOnlyList<MonthlyPointDto>> MonthlyAsync(Guid userId, DateTime start, DateTime end, CancellationToken ct)
    {
        var views = await db.ListingViews.Where(v => v.Listing.SellerId == userId && v.ViewedAt >= start && v.ViewedAt <= end)
            .GroupBy(v => new { v.ViewedAt.Year, v.ViewedAt.Month }).Select(g => new { g.Key.Year, g.Key.Month, N = g.Count() }).ToListAsync(ct);
        var clicks = await db.ContactEvents.Where(c => c.Listing.SellerId == userId && c.CreatedAt >= start && c.CreatedAt <= end)
            .GroupBy(c => new { c.CreatedAt.Year, c.CreatedAt.Month }).Select(g => new { g.Key.Year, g.Key.Month, N = g.Count() }).ToListAsync(ct);
        var props = await db.Negotiations.Where(n => n.SellerId == userId && n.CreatedAt >= start && n.CreatedAt <= end)
            .GroupBy(n => new { n.CreatedAt.Year, n.CreatedAt.Month }).Select(g => new { g.Key.Year, g.Key.Month, N = g.Count() }).ToListAsync(ct);
        var hist = await db.ListingStatusHistory
            .Where(h => (h.ToStatus == ListingStatus.Sold || h.ToStatus == ListingStatus.Rented) && h.ChangedAt >= start && h.ChangedAt <= end
                        && db.Listings.IgnoreQueryFilters().Any(l => l.Id == h.ListingId && l.SellerId == userId))
            .GroupBy(h => new { h.ChangedAt.Year, h.ChangedAt.Month, h.ToStatus }).Select(g => new { g.Key.Year, g.Key.Month, g.Key.ToStatus, N = g.Count() }).ToListAsync(ct);

        var points = new List<MonthlyPointDto>();
        for (var m = new DateTime(start.Year, start.Month, 1); m <= end; m = m.AddMonths(1))
        {
            int N<T>(IEnumerable<T> src, Func<T, bool> where, Func<T, int> n) => src.Where(where).Sum(n);
            points.Add(new MonthlyPointDto($"{m:yyyy-MM}",
                views.Where(x => x.Year == m.Year && x.Month == m.Month).Sum(x => x.N),
                clicks.Where(x => x.Year == m.Year && x.Month == m.Month).Sum(x => x.N),
                props.Where(x => x.Year == m.Year && x.Month == m.Month).Sum(x => x.N),
                hist.Where(x => x.Year == m.Year && x.Month == m.Month && x.ToStatus == ListingStatus.Sold).Sum(x => x.N),
                hist.Where(x => x.Year == m.Year && x.Month == m.Month && x.ToStatus == ListingStatus.Rented).Sum(x => x.N)));
        }
        return points;
    }

    /// <summary>División segura: 0 cuando el denominador es 0. Resultado redondeado a 4 decimales (0..1).</summary>
    public static double SafeRate(int numerator, int denominator) => denominator <= 0 ? 0 : Math.Round(numerator / (double)denominator, 4);
}
