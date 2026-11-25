using MotorLink.Domain.Common;

namespace MotorLink.Domain.Rules;

/// <summary>Máquina de estados de las publicaciones (reglas puras, sin dependencias).</summary>
public static class ListingStateMachine
{
    private static readonly Dictionary<ListingStatus, ListingStatus[]> Transitions = new()
    {
        [ListingStatus.Draft] = [ListingStatus.PendingReview, ListingStatus.Archived],
        [ListingStatus.PendingReview] = [ListingStatus.Published, ListingStatus.Rejected, ListingStatus.Draft],
        [ListingStatus.Rejected] = [ListingStatus.Draft, ListingStatus.Archived],
        [ListingStatus.Published] = [ListingStatus.Reserved, ListingStatus.Rented, ListingStatus.Sold, ListingStatus.Paused, ListingStatus.Archived],
        [ListingStatus.Reserved] = [ListingStatus.Published, ListingStatus.Rented, ListingStatus.Sold, ListingStatus.Archived],
        [ListingStatus.Rented] = [ListingStatus.Published, ListingStatus.Sold, ListingStatus.Archived],
        [ListingStatus.Paused] = [ListingStatus.Published, ListingStatus.Archived],
        [ListingStatus.Sold] = [ListingStatus.Archived],
        [ListingStatus.Archived] = [ListingStatus.Draft]
    };

    /// <summary>Estados que consumen el cupo de "publicaciones activas" del plan.</summary>
    public static readonly ListingStatus[] CountsTowardLimit =
        [ListingStatus.PendingReview, ListingStatus.Published, ListingStatus.Reserved, ListingStatus.Rented];

    /// <summary>Estados visibles al público (Reservado/Alquilado se muestran como no disponibles).</summary>
    public static readonly ListingStatus[] PublicVisible = [ListingStatus.Published, ListingStatus.Reserved, ListingStatus.Rented];

    /// <summary>Estados en los que el propietario puede editar la publicación.</summary>
    public static readonly ListingStatus[] Editable = [ListingStatus.Draft, ListingStatus.Rejected, ListingStatus.Paused];

    public static bool CanTransition(ListingStatus from, ListingStatus to) =>
        Transitions.TryGetValue(from, out var allowed) && allowed.Contains(to);

    /// <summary>Solo moderación (ADMIN) aprueba o rechaza.</summary>
    public static bool RequiresModerator(ListingStatus from, ListingStatus to) =>
        from == ListingStatus.PendingReview && to is ListingStatus.Published or ListingStatus.Rejected;

    public static bool IsRentalOperation(OperationType t) => t is OperationType.Rent or OperationType.WorkRent;
}

public static class VehicleTypeCodes
{
    public const string Car = "CAR";
    public const string Motorcycle = "MOTORCYCLE";
    public const string Pickup = "PICKUP";
    public const string Truck = "TRUCK";
    public const string Van = "VAN";
    public const string Mototaxi = "MOTOTAXI";
    public const string Minivan = "MINIVAN";
    public const string Bus = "BUS";
}
