using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using MotorLink.Application.Common;
using MotorLink.Application.Notifications;
using MotorLink.Domain.Common;
using MotorLink.Domain.Entities;
using MotorLink.Domain.Exceptions;

namespace MotorLink.Application.Negotiations;

public sealed record StartNegotiationRequest
{
    public Guid ListingId { get; init; }
    public OperationType OperationType { get; init; }
    [Range(0.01, 1_000_000_000)] public decimal Amount { get; init; }
    /// <summary>Obligatoria para alquileres (DAILY/WEEKLY/MONTHLY); debe omitirse en ventas.</summary>
    public RentPeriod? Periodicity { get; init; }
    /// <summary>Opcional; si se envía debe coincidir con la moneda de la modalidad.</summary>
    public Currency? Currency { get; init; }
    [StringLength(1000)] public string? Message { get; init; }
}

public sealed record CounterOfferRequest
{
    [Range(0.01, 1_000_000_000)] public decimal Amount { get; init; }
    [StringLength(1000)] public string? Message { get; init; }
}

public sealed record AcceptOfferRequest([Required] Guid OfferId);

public sealed record OfferDto(Guid Id, int Sequence, Guid OfferedById, decimal Amount, Currency Currency, RentPeriod? Periodicity, string? Message, DateTime CreatedAt);

public sealed record NegotiationDto(
    Guid Id, Guid ListingId, string ListingTitle, OperationType OperationType, Guid BuyerId, string BuyerName, Guid SellerId, string SellerName,
    NegotiationStatus Status, Currency Currency, RentPeriod? Periodicity, decimal InitialAmount, decimal LastAmount, Guid LastOfferById,
    DateTime CreatedAt, DateTime? UpdatedAt, DateTime ExpiresAt);

public sealed record NegotiationDetailDto(
    NegotiationDto Negotiation, decimal? ListedPrice, decimal? AgreedAmount, bool AwaitingMyResponse, IReadOnlyList<OfferDto> Offers);

public enum NegotiationRole { All = 0, Sent = 1, Received = 2 }

public interface INegotiationService
{
    Task<NegotiationDetailDto> StartAsync(Guid buyerId, StartNegotiationRequest r, CancellationToken ct);
    Task<PagedResult<NegotiationDto>> ListAsync(Guid userId, NegotiationRole role, NegotiationStatus? status, int page, int pageSize, CancellationToken ct);
    Task<NegotiationDetailDto> GetAsync(Guid userId, Guid id, CancellationToken ct);
    Task<NegotiationDetailDto> CounterAsync(Guid userId, Guid id, CounterOfferRequest r, CancellationToken ct);
    Task<NegotiationDetailDto> AcceptAsync(Guid userId, Guid id, Guid offerId, CancellationToken ct);
    Task<NegotiationDetailDto> RejectAsync(Guid userId, Guid id, CancellationToken ct);
    Task<NegotiationDetailDto> CancelAsync(Guid userId, Guid id, CancellationToken ct);
    Task<int> ExpireDueAsync(CancellationToken ct);
}

/// <summary>
/// Propuestas y contrapropuestas. Cada oferta es un registro inmutable. La concurrencia se controla con una transacción
/// y el token optimista (RowVersion) de la negociación: dos respuestas simultáneas no pueden ambas prosperar.
/// Una negociación aceptada es un acuerdo de PRECIO, no confirma pago ni entrega.
/// </summary>
public sealed class NegotiationService(IApplicationDbContext db, INotificationService notifications, TimeProvider clock) : INegotiationService
{
    public static readonly TimeSpan OfferLifetime = TimeSpan.FromDays(7);
    private static readonly NegotiationStatus[] Open = [NegotiationStatus.Pending, NegotiationStatus.CounterOffer];
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public Task<NegotiationDetailDto> StartAsync(Guid buyerId, StartNegotiationRequest r, CancellationToken ct) =>
        db.ExecuteInTransactionAsync(null, async () =>
        {
            var listing = await db.Listings.AsNoTracking().Where(l => l.Id == r.ListingId)
                .Select(l => new { l.Id, l.Title, l.SellerId, l.Status }).FirstOrDefaultAsync(ct)
                ?? throw new NotFoundException("Publicación no encontrada.");
            if (listing.SellerId == buyerId) throw new ConflictException("No puedes negociar tu propia publicación.");
            if (listing.Status != ListingStatus.Published) throw new ConflictException("La publicación no está disponible para negociar.");

            var op = await db.ListingOperations.AsNoTracking().FirstOrDefaultAsync(
                o => o.ListingId == r.ListingId && o.OperationType == r.OperationType && o.IsActive, ct)
                ?? throw BusinessValidationException.For(nameof(r.OperationType), "La publicación no ofrece esa modalidad.");
            if (op.Status != OperationStatus.Available) throw new ConflictException("Esa modalidad ya no está disponible.");

            if (r.Currency is { } c && c != op.Currency)
                throw BusinessValidationException.For(nameof(r.Currency), $"La moneda debe ser {op.Currency}, la de la modalidad publicada.");

            var listed = ListedPrice(op, r.Periodicity, r.OperationType, out var error);
            if (error is not null) throw BusinessValidationException.For(nameof(r.Periodicity), error);

            if (await db.Negotiations.AnyAsync(n => n.ListingOperationId == op.Id && n.BuyerId == buyerId && Open.Contains(n.Status), ct))
                throw new ConflictException("Ya tienes una negociación abierta para esta modalidad.");

            var now = Now;
            var n = new Negotiation
            {
                Id = Guid.NewGuid(), ListingId = listing.Id, ListingOperationId = op.Id, BuyerId = buyerId, SellerId = listing.SellerId,
                Status = NegotiationStatus.Pending, Currency = op.Currency, Periodicity = r.Periodicity, InitialAmount = r.Amount,
                ExpiresAt = now + OfferLifetime, LastOfferSequence = 1
            };
            n.Offers.Add(NewOffer(n.Id, 1, buyerId, r.Amount, op.Currency, r.Periodicity, r.Message));
            db.Negotiations.Add(n);
            notifications.Notify(listing.SellerId, "NEGOTIATION_OFFER", "Nueva propuesta",
                $"Recibiste una propuesta de {Money(op.Currency, r.Amount)} por «{listing.Title}».", n.Id);
            await db.SaveChangesAsync(ct);
            return await DetailAsync(buyerId, n.Id, ct);
        }, ct);

    public async Task<PagedResult<NegotiationDto>> ListAsync(Guid userId, NegotiationRole role, NegotiationStatus? status, int page, int pageSize, CancellationToken ct) =>
        await Summaries(db.Negotiations.AsNoTracking()
            .Where(n => role == NegotiationRole.Sent ? n.BuyerId == userId : role == NegotiationRole.Received ? n.SellerId == userId : (n.BuyerId == userId || n.SellerId == userId))
            .Where(n => status == null || n.Status == status)
            .OrderByDescending(n => n.UpdatedAt ?? n.CreatedAt)).ToPagedAsync(page, pageSize, ct);

    public Task<NegotiationDetailDto> GetAsync(Guid userId, Guid id, CancellationToken ct) => DetailAsync(userId, id, ct);

    public Task<NegotiationDetailDto> CounterAsync(Guid userId, Guid id, CounterOfferRequest r, CancellationToken ct) =>
        Mutate(userId, id, ct, async (n, last) =>
        {
            if (last.OfferedById == userId) throw new ConflictException("Es el turno de la contraparte: no puedes enviar otra propuesta seguida.");
            await EnsureNegotiableAsync(n, ct);
            if (r.Amount <= 0) throw BusinessValidationException.For(nameof(r.Amount), "El monto debe ser mayor a cero.");

            var seq = n.LastOfferSequence + 1;
            // Moneda y periodicidad se heredan de la negociación: no pueden variar entre ofertas.
            db.NegotiationOffers.Add(NewOffer(n.Id, seq, userId, r.Amount, n.Currency, n.Periodicity, r.Message));
            n.LastOfferSequence = seq; n.Status = NegotiationStatus.CounterOffer; n.ExpiresAt = Now + OfferLifetime;
            notifications.Notify(Other(n, userId), "NEGOTIATION_COUNTER", "Nueva contrapropuesta",
                $"Recibiste una contrapropuesta de {Money(n.Currency, r.Amount)}.", n.Id);
        });

    public Task<NegotiationDetailDto> AcceptAsync(Guid userId, Guid id, Guid offerId, CancellationToken ct) =>
        Mutate(userId, id, ct, async (n, last) =>
        {
            if (last.Id != offerId) throw new ConflictException("Solo se puede aceptar la última oferta vigente.");
            if (last.OfferedById == userId) throw new ConflictException("No puedes aceptar tu propia oferta; solo la contraparte puede hacerlo.");
            await EnsureNegotiableAsync(n, ct);
            n.Status = NegotiationStatus.Accepted;
            notifications.Notify(Other(n, userId), "NEGOTIATION_ACCEPTED", "Propuesta aceptada",
                $"Se aceptó la oferta de {Money(n.Currency, last.Amount)}. Es un acuerdo de precio; el pago y la entrega se coordinan directamente entre las partes.", n.Id);
        });

    public Task<NegotiationDetailDto> RejectAsync(Guid userId, Guid id, CancellationToken ct) =>
        Mutate(userId, id, ct, (n, last) =>
        {
            if (last.OfferedById == userId) throw new ConflictException("No puedes rechazar tu propia oferta; puedes cancelarla.");
            n.Status = NegotiationStatus.Rejected;
            notifications.Notify(Other(n, userId), "NEGOTIATION_REJECTED", "Propuesta rechazada", "Tu propuesta fue rechazada.", n.Id);
            return Task.CompletedTask;
        });

    public Task<NegotiationDetailDto> CancelAsync(Guid userId, Guid id, CancellationToken ct) =>
        Mutate(userId, id, ct, (n, last) =>
        {
            if (userId != n.BuyerId && last.OfferedById != userId)
                throw new ConflictException("El vendedor solo puede cancelar su propia contrapropuesta; para la oferta del comprador use el rechazo.");
            n.Status = NegotiationStatus.Cancelled;
            notifications.Notify(Other(n, userId), "NEGOTIATION_CANCELLED", "Negociación cancelada", "La contraparte canceló la negociación.", n.Id);
            return Task.CompletedTask;
        });

    public async Task<int> ExpireDueAsync(CancellationToken ct)
    {
        var now = Now;
        var due = await db.Negotiations.Where(n => Open.Contains(n.Status) && n.ExpiresAt <= now).Take(200).ToListAsync(ct);
        foreach (var n in due)
        {
            n.Status = NegotiationStatus.Expired;
            notifications.Notify(n.BuyerId, "NEGOTIATION_EXPIRED", "Negociación vencida", "La negociación venció sin respuesta.", n.Id);
            notifications.Notify(n.SellerId, "NEGOTIATION_EXPIRED", "Negociación vencida", "La negociación venció sin respuesta.", n.Id);
        }
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { /* otra solicitud la modificó: se reintentará en el próximo ciclo */ }
        return due.Count;
    }

    // ---------------- núcleo ----------------
    /// <summary>Carga la negociación abierta, valida participante/estado, ejecuta la mutación y devuelve el detalle actualizado.</summary>
    private Task<NegotiationDetailDto> Mutate(Guid userId, Guid id, CancellationToken ct, Func<Negotiation, NegotiationOffer, Task> mutation) =>
        db.ExecuteInTransactionAsync(null, async () =>
        {
            var n = await db.Negotiations.FirstOrDefaultAsync(x => x.Id == id && (x.BuyerId == userId || x.SellerId == userId), ct)
                ?? throw new NotFoundException("Negociación no encontrada."); // no participante => 404 (no revela existencia)

            if (!Open.Contains(n.Status)) throw new ConflictException($"La negociación ya finalizó ({n.Status}) y no puede modificarse.");
            if (n.ExpiresAt <= Now)
            {
                n.Status = NegotiationStatus.Expired;
                await db.SaveChangesAsync(ct);
                throw new ConflictException("La negociación expiró.");
            }

            var last = await db.NegotiationOffers.Where(o => o.NegotiationId == id).OrderByDescending(o => o.Sequence).FirstAsync(ct);
            await mutation(n, last);
            await db.SaveChangesAsync(ct); // RowVersion: una modificación simultánea lanza DbUpdateConcurrencyException (409)
            return await DetailAsync(userId, id, ct);
        }, ct);

    private async Task EnsureNegotiableAsync(Negotiation n, CancellationToken ct)
    {
        var state = await db.ListingOperations.AsNoTracking().Where(o => o.Id == n.ListingOperationId)
            .Select(o => new { o.IsActive, o.Status, ListingStatus = o.Listing.Status }).FirstAsync(ct);
        if (state.ListingStatus != ListingStatus.Published || !state.IsActive || state.Status != OperationStatus.Available)
            throw new ConflictException("La publicación ya no está activa; no se pueden registrar más propuestas.");
    }

    private async Task<NegotiationDetailDto> DetailAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var dto = await Summaries(db.Negotiations.AsNoTracking().Where(n => n.Id == id && (n.BuyerId == userId || n.SellerId == userId))).FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Negociación no encontrada.");
        var offers = await db.NegotiationOffers.AsNoTracking().Where(o => o.NegotiationId == id).OrderBy(o => o.Sequence)
            .Select(o => new OfferDto(o.Id, o.Sequence, o.OfferedById, o.Amount, o.Currency, o.Periodicity, o.Message, o.CreatedAt)).ToListAsync(ct);

        var op = await db.ListingOperations.AsNoTracking().Where(o => o.Id == db.Negotiations.Where(n => n.Id == id).Select(n => n.ListingOperationId).First()).FirstAsync(ct);
        var listed = ListedPrice(op, dto.Periodicity, dto.OperationType, out _);
        var open = dto.Status is NegotiationStatus.Pending or NegotiationStatus.CounterOffer;
        return new NegotiationDetailDto(dto, listed, dto.Status == NegotiationStatus.Accepted ? dto.LastAmount : null,
            open && dto.LastOfferById != userId, offers);
    }

    private static IQueryable<NegotiationDto> Summaries(IQueryable<Negotiation> q) => q.Select(n => new NegotiationDto(
        n.Id, n.ListingId, n.Listing.Title, n.ListingOperation.OperationType,
        n.BuyerId, n.Buyer.Profile != null ? n.Buyer.Profile.FirstName + " " + n.Buyer.Profile.LastName.Substring(0, 1) + "." : "Usuario",
        n.SellerId, n.Seller.Profile != null ? n.Seller.Profile.FirstName + " " + n.Seller.Profile.LastName.Substring(0, 1) + "." : "Usuario",
        n.Status, n.Currency, n.Periodicity, n.InitialAmount,
        n.Offers.OrderByDescending(o => o.Sequence).Select(o => o.Amount).FirstOrDefault(),
        n.Offers.OrderByDescending(o => o.Sequence).Select(o => o.OfferedById).FirstOrDefault(),
        n.CreatedAt, n.UpdatedAt, n.ExpiresAt));

    private static decimal? ListedPrice(ListingOperation op, RentPeriod? period, OperationType type, out string? error)
    {
        error = null;
        if (type == OperationType.Sale)
        {
            if (period is not null) error = "Las ventas no tienen periodicidad.";
            return op.SalePrice;
        }
        if (period is null) { error = "Indique la periodicidad (DAILY, WEEKLY o MONTHLY)."; return null; }
        var price = period switch { RentPeriod.Daily => op.DailyRate, RentPeriod.Weekly => op.WeeklyRate, _ => op.MonthlyRate };
        if (price is null) error = "La modalidad no publica tarifa para esa periodicidad.";
        return price;
    }

    private NegotiationOffer NewOffer(Guid negotiationId, int seq, Guid by, decimal amount, Currency currency, RentPeriod? period, string? message) => new()
    {
        Id = Guid.NewGuid(), NegotiationId = negotiationId, Sequence = seq, OfferedById = by, Amount = amount,
        Currency = currency, Periodicity = period, Message = message?.Trim()
    };

    private static Guid Other(Negotiation n, Guid me) => n.BuyerId == me ? n.SellerId : n.BuyerId;
    private static string Money(Currency c, decimal v) => $"{(c == Currency.Pen ? "S/" : "US$")} {v:0.##}";
}
