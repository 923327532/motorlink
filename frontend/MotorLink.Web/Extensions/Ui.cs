using System.Globalization;
using MotorLink.Web.Services.Api;

namespace MotorLink.Web.Extensions;

/// <summary>Formato y etiquetas en español para los valores (enums MAYÚSCULAS) que devuelve la API.</summary>
public static class Ui
{
    private static readonly CultureInfo Pe = new("es-PE");

    // ---------- Dinero y fechas ----------
    public static string Symbol(string? currency) => currency == "USD" ? "US$" : "S/";

    public static string Money(string? currency, decimal? amount)
    {
        if (amount is null) return "—";
        var n = amount.Value == decimal.Truncate(amount.Value) ? amount.Value.ToString("N0", Pe) : amount.Value.ToString("N2", Pe);
        return $"{Symbol(currency)} {n}";
    }

    public static string Date(DateTime? d) => d is null ? "—" : d.Value.ToLocalTime().ToString("d MMM yyyy", Pe).Replace(".", "");
    public static string DateTimeText(DateTime? d) => d is null ? "—" : d.Value.ToLocalTime().ToString("d MMM yyyy, HH:mm", Pe).Replace(".", "");
    public static string Number(int n) => n.ToString("N0", Pe);
    public static string Percent(double rate) => (rate * 100).ToString("0.#", Pe) + " %";

    public static string TimeAgo(DateTime d)
    {
        var span = DateTime.UtcNow - d.ToUniversalTime();
        if (span.TotalMinutes < 1) return "ahora";
        if (span.TotalMinutes < 60) return $"hace {(int)span.TotalMinutes} min";
        if (span.TotalHours < 24) return $"hace {(int)span.TotalHours} h";
        if (span.TotalDays < 7) return $"hace {(int)span.TotalDays} d";
        return Date(d);
    }

    // ---------- Precio según modalidad ----------
    public sealed record PriceView(string Amount, string? Suffix, string OperationType);

    /// <summary>Venta: "S/ 58,000". Alquiler: "S/ 120 por día". Trabajar: "S/ 65 por día".</summary>
    public static PriceView? Price(IEnumerable<OperationSummary> ops, string? preferred = null)
    {
        var list = ops.ToList();
        var op = (preferred is not null ? list.FirstOrDefault(o => o.OperationType == preferred) : null)
                 ?? list.FirstOrDefault(o => o.OperationType == "SALE") ?? list.FirstOrDefault(o => o.OperationType == "RENT") ?? list.FirstOrDefault();
        if (op is null) return null;
        if (op.OperationType == "SALE") return new PriceView(Money(op.Currency, op.SalePrice), null, op.OperationType);
        if (op.DailyRate is not null) return new PriceView(Money(op.Currency, op.DailyRate), "por día", op.OperationType);
        if (op.WeeklyRate is not null) return new PriceView(Money(op.Currency, op.WeeklyRate), "por semana", op.OperationType);
        return new PriceView(Money(op.Currency, op.MonthlyRate), "por mes", op.OperationType);
    }

    // ---------- Etiquetas ----------
    public static string Operation(string? v) => v switch { "SALE" => "Venta", "RENT" => "Alquiler", "WORK_RENT" => "Para trabajar", _ => v ?? "" };
    public static string Condition(string? v) => v switch { "NEW" => "Nuevo", "SEMI_NEW" => "Seminuevo", "USED" => "Usado", _ => v ?? "" };
    public static string Fuel(string? v) => v switch { "GASOLINE" => "Gasolina", "DIESEL" => "Diésel", "LPG" => "GLP", "CNG" => "GNV", "HYBRID" => "Híbrido", "ELECTRIC" => "Eléctrico", _ => v ?? "" };
    public static string Transmission(string? v) => v switch { "MANUAL" => "Manual", "AUTOMATIC" => "Automática", _ => v ?? "" };
    public static string Traction(string? v) => v switch { "FWD" => "Delantera (4x2)", "RWD" => "Trasera (4x2)", "AWD" => "Integral (AWD)", "FOUR_BY_FOUR" => "4x4", _ => v ?? "—" };
    public static string WorkUse(string? v) => v switch { "TAXI" => "Taxi", "DELIVERY" => "Reparto", "TRANSPORT" => "Transporte", "CARGO" => "Carga", _ => v ?? "" };
    public static string Shift(string? v) => v switch { "DAY" => "Día", "NIGHT" => "Noche", "FULL" => "Turno completo", _ => v ?? "" };
    public static string Bearer(string? v) => v switch { "OWNER" => "Propietario", "DRIVER" => "Conductor", "SHARED" => "Compartido", _ => v ?? "" };
    public static string Period(string? v) => v switch { "DAILY" => "por día", "WEEKLY" => "por semana", "MONTHLY" => "por mes", _ => "" };
    public static string ProfileType(string? v) => v switch { "BUSINESS" => "Empresarial", _ => "Personal" };

    public static string ListingStatus(string? v) => v switch
    {
        "DRAFT" => "Borrador", "PENDING_REVIEW" => "En revisión", "PUBLISHED" => "Publicada", "RESERVED" => "Reservada", "SOLD" => "Vendida",
        "RENTED" => "Alquilada", "PAUSED" => "Pausada", "REJECTED" => "Rechazada", "ARCHIVED" => "Archivada", _ => v ?? ""
    };

    public static string NegotiationStatus(string? v) => v switch
    {
        "PENDING" => "Pendiente", "COUNTER_OFFER" => "Contrapropuesta", "ACCEPTED" => "Acuerdo de precio", "REJECTED" => "Rechazada",
        "CANCELLED" => "Cancelada", "EXPIRED" => "Vencida", _ => v ?? ""
    };

    public static string SubscriptionStatus(string? v) => v switch { "ACTIVE" => "Activa", "PENDING" => "Pendiente de pago", "EXPIRED" => "Vencida", "CANCELLED" => "Cancelada", _ => v ?? "" };
    public static string PaymentStatus(string? v) => v switch { "PAID" => "Pagado", "PENDING" => "Pendiente", "FAILED" => "Fallido", "REFUNDED" => "Reembolsado", _ => v ?? "" };
    public static string PaymentMode(string? v) => v switch { "SIMULATED" => "Demostración", "REAL" => "Pago real", "COMPLIMENTARY" => "Otorgado por administración", _ => "Sin pago" };

    /// <summary>Clase de badge (success, danger, warning, info, dark o vacío) por estado.</summary>
    public static string Tone(string? status) => status switch
    {
        "PUBLISHED" or "ACTIVE" or "ACCEPTED" or "PAID" or "AVAILABLE" => "success",
        "REJECTED" or "FAILED" or "CANCELLED" or "EXPIRED" or "SOLD" => "danger",
        "PENDING_REVIEW" or "PENDING" or "PAUSED" or "COUNTER_OFFER" or "RESERVED" => "warning",
        "RENTED" or "REFUNDED" => "info",
        _ => ""
    };

    public static string Initials(string? name)
    {
        var parts = (name ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 0 ? "?" : string.Concat(parts.Take(2).Select(p => char.ToUpperInvariant(p[0])));
    }

    /// <summary>Las imágenes locales (/media/...) o de Cloudinary se usan tal cual; sin foto se muestra el marcador.</summary>
    public static string Img(string? url) => string.IsNullOrWhiteSpace(url) ? "/images/placeholder-vehicle.svg" : url;
}
