using Microsoft.EntityFrameworkCore;
using MotorLink.Application.Common;
using MotorLink.Application.Notifications;
using MotorLink.Application.Subscriptions;
using MotorLink.Domain.Common;
using MotorLink.Domain.Entities;
using MotorLink.Domain.Exceptions;
using MotorLink.Domain.Rules;

namespace MotorLink.Application.Listings;

/// <summary>Aplica transiciones de estado con todas sus reglas y efectos (operaciones, historial, notificaciones, límites).</summary>
public interface IListingLifecycle
{
    Task TransitionAsync(Guid listingId, ListingStatus to, OperationType? operation, Guid actorId, bool asModerator, string? note, CancellationToken ct);
}

public sealed class ListingLifecycle(
    IApplicationDbContext db, ISubscriptionPermissions permissions, INotificationService notifications,
    IAuditService audit, TimeProvider clock) : IListingLifecycle
{
    public async Task TransitionAsync(Guid listingId, ListingStatus to, OperationType? operation, Guid actorId, bool asModerator, string? note, CancellationToken ct)
    {
        // Se bloquea al VENDEDOR (dueño del cupo del plan) durante toda la transición.
        var sellerId = await db.Listings.IgnoreQueryFilters().Where(l => l.Id == listingId && !l.IsDeleted).Select(l => (Guid?)l.SellerId).FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Publicación no encontrada.");

        await db.ExecuteInTransactionAsync(sellerId, async () =>
        {
            var listing = await db.Listings.Include(l => l.Operations).Include(l => l.Vehicle).FirstOrDefaultAsync(l => l.Id == listingId, ct)
                ?? throw new NotFoundException("Publicación no encontrada.");
            if (!asModerator && listing.SellerId != actorId) throw new NotFoundException("Publicación no encontrada.");

            var from = listing.Status;
            if (!ListingStateMachine.CanTransition(from, to))
                throw new ConflictException($"Transición no permitida: {from} → {to}.");
            if (ListingStateMachine.RequiresModerator(from, to) && !asModerator)
                throw new ForbiddenException("Solo un moderador puede aprobar o rechazar publicaciones.");
            if (asModerator && !ListingStateMachine.RequiresModerator(from, to))
                throw new ConflictException("La moderación solo aprueba o rechaza publicaciones pendientes de revisión.");

            var now = clock.GetUtcNow().UtcDateTime;
            var activeOps = listing.Operations.Where(o => o.IsActive).ToList();
            ListingOperation? committed = null;

            switch (to)
            {
                case ListingStatus.PendingReview:
                    await ValidateReadyForReviewAsync(listing, activeOps, ct);
                    await permissions.EnsureCanActivateListingAsync(listing.SellerId, listing.Id, ct);
                    break;

                case ListingStatus.Published when from == ListingStatus.PendingReview:
                    listing.PublishedAt ??= now; listing.ReviewedAt = now; listing.ReviewedById = actorId; listing.RejectionReason = null;
                    break;

                case ListingStatus.Published when from == ListingStatus.Paused:
                    await permissions.EnsureCanActivateListingAsync(listing.SellerId, listing.Id, ct);
                    break;

                case ListingStatus.Published: // Reserved/Rented -> Published: la modalidad vuelve a estar disponible
                    foreach (var o in activeOps.Where(o => o.Status is OperationStatus.Reserved or OperationStatus.Rented)) o.Status = OperationStatus.Available;
                    listing.CommittedOperationId = null;
                    break;

                case ListingStatus.Rejected:
                    if (string.IsNullOrWhiteSpace(note)) throw BusinessValidationException.For("reason", "Debe indicar el motivo del rechazo.");
                    listing.ReviewedAt = now; listing.ReviewedById = actorId; listing.RejectionReason = note.Trim();
                    break;

                case ListingStatus.Reserved:
                case ListingStatus.Rented:
                    committed = RequireOperation(activeOps, operation, to);
                    if (to == ListingStatus.Rented && !ListingStateMachine.IsRentalOperation(committed.OperationType))
                        throw BusinessValidationException.For("operationType", "Solo las modalidades de alquiler pueden marcarse como alquiladas.");
                    // Una modalidad comprometida bloquea a las demás: la publicación deja de figurar como disponible.
                    foreach (var o in activeOps) o.Status = OperationStatus.Available;
                    committed.Status = to == ListingStatus.Reserved ? OperationStatus.Reserved : OperationStatus.Rented;
                    listing.CommittedOperationId = committed.Id;
                    listing.IsFeatured = false;
                    break;

                case ListingStatus.Sold:
                    // Al venderse el vehículo se cierran todas sus modalidades comerciales activas.
                    var sale = activeOps.FirstOrDefault(o => o.OperationType == OperationType.Sale);
                    foreach (var o in activeOps) o.Status = o == sale ? OperationStatus.Sold : OperationStatus.Closed;
                    listing.CommittedOperationId = sale?.Id;
                    listing.IsFeatured = false;
                    await CancelOpenNegotiationsAsync(listing, "El vehículo fue vendido.", ct);
                    break;

                case ListingStatus.Paused:
                case ListingStatus.Archived:
                    listing.IsFeatured = false;
                    if (to == ListingStatus.Archived) await CancelOpenNegotiationsAsync(listing, "La publicación fue archivada.", ct);
                    else await CancelOpenNegotiationsAsync(listing, "La publicación fue pausada.", ct);
                    break;
            }

            listing.Status = to;
            db.ListingStatusHistory.Add(new ListingStatusHistory
            {
                ListingId = listing.Id, FromStatus = from, ToStatus = to, OperationType = committed?.OperationType ?? operation,
                ChangedById = actorId, ChangedAt = now, Note = note
            });

            if (asModerator)
            {
                audit.Log(to == ListingStatus.Published ? "listing.approve" : "listing.reject", nameof(Listing), listing.Id.ToString(), note);
                notifications.Notify(listing.SellerId, to == ListingStatus.Published ? "LISTING_APPROVED" : "LISTING_REJECTED",
                    to == ListingStatus.Published ? "Publicación aprobada" : "Publicación rechazada",
                    to == ListingStatus.Published ? $"«{listing.Title}» ya está publicada." : $"«{listing.Title}» fue rechazada: {note}", listing.Id);
            }

            await db.SaveChangesAsync(ct);
            return true;
        }, ct);
    }

    private static ListingOperation RequireOperation(List<ListingOperation> activeOps, OperationType? type, ListingStatus to)
    {
        if (type is null) throw BusinessValidationException.For("operationType", $"Indique la modalidad para marcar la publicación como {to}.");
        return activeOps.FirstOrDefault(o => o.OperationType == type)
            ?? throw BusinessValidationException.For("operationType", "La publicación no tiene esa modalidad activa.");
    }

    private async Task ValidateReadyForReviewAsync(Listing l, List<ListingOperation> activeOps, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        if (activeOps.Count == 0) errors["operations"] = ["Agregue al menos una modalidad (venta o alquiler)."];
        if (string.IsNullOrWhiteSpace(l.PhoneNumber) && string.IsNullOrWhiteSpace(l.WhatsAppNumber)) errors["contact"] = ["Indique un teléfono o WhatsApp de contacto."];
        if (!await db.VehicleImages.AnyAsync(i => i.VehicleId == l.VehicleId, ct)) errors["photos"] = ["Cargue al menos una fotografía del vehículo."];
        if (l.AdvertiserType == ProfileType.Business) await permissions.RequireFeatureAsync(l.SellerId, PlanFeature.BusinessProfile, ct);
        if (errors.Count > 0) throw new BusinessValidationException(errors);
    }

    /// <summary>Las negociaciones abiertas no pueden continuar sobre una publicación no disponible.</summary>
    private async Task CancelOpenNegotiationsAsync(Listing l, string reason, CancellationToken ct)
    {
        var open = new[] { NegotiationStatus.Pending, NegotiationStatus.CounterOffer };
        foreach (var n in await db.Negotiations.Where(n => n.ListingId == l.Id && open.Contains(n.Status)).ToListAsync(ct))
        {
            n.Status = NegotiationStatus.Cancelled;
            notifications.Notify(n.BuyerId, "NEGOTIATION_CANCELLED", "Negociación cancelada", $"«{l.Title}»: {reason}", n.Id);
        }
    }
}
