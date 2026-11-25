using MotorLink.Domain.Common;

namespace MotorLink.Domain.Entities;

public class Vehicle : SoftDeletableEntity<Guid>
{
    /// <summary>Usuario que registró el vehículo (anunciante).</summary>
    public Guid OwnerId { get; set; }
    public int VehicleTypeId { get; set; }
    public int ModelId { get; set; }
    public string? Version { get; set; }
    public int ManufactureYear { get; set; }
    public int ModelYear { get; set; }
    /// <summary>Placa normalizada (MAYÚSCULAS, solo letras y dígitos). Null solo en vehículos nuevos sin matrícula.</summary>
    public string? Plate { get; set; }
    public string? Color { get; set; }
    public int Mileage { get; set; }
    public VehicleCondition Condition { get; set; }
    public int PreviousOwners { get; set; }
    public FuelType FuelType { get; set; }
    public TransmissionType Transmission { get; set; }
    public int? EngineDisplacementCc { get; set; }
    public int? Doors { get; set; }
    public int? Seats { get; set; }
    public TractionType? Traction { get; set; }
    public int? LoadCapacityKg { get; set; }
    public bool HasAirConditioning { get; set; }
    public bool HasGps { get; set; }
    public bool HasInsurance { get; set; }
    public bool HasValidSoat { get; set; }
    public bool HasValidTechnicalReview { get; set; }
    public string? Description { get; set; }

    /// <summary>Propietario legal DECLARADO por el anunciante cuando es otra persona. No está verificado.</summary>
    public string? LegalOwnerName { get; set; }
    public string? LegalOwnerRelationship { get; set; }
    /// <summary>Siempre false: no existe todavía un proceso de verificación de titularidad.</summary>
    public bool OwnershipVerified { get; set; }

    public ApplicationUser Owner { get; set; } = null!;
    public VehicleType VehicleType { get; set; } = null!;
    public VehicleModel Model { get; set; } = null!;
    public ICollection<VehicleImage> Images { get; set; } = new List<VehicleImage>();
}

public class VehicleImage : AuditableEntity<Guid>
{
    public Guid VehicleId { get; set; }
    public string Url { get; set; } = string.Empty;
    /// <summary>Identificador del archivo en el proveedor (Cloudinary public_id).</summary>
    public string ProviderPublicId { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public int SortOrder { get; set; }
    public bool IsCover { get; set; }
    public Vehicle Vehicle { get; set; } = null!;
}

/// <summary>Archivo subido al proveedor cuyo borrado o registro falló; un proceso periódico reintenta eliminarlo.</summary>
public class OrphanedImage
{
    public long Id { get; set; }
    public string ProviderPublicId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }
}

public class Listing : SoftDeletableEntity<Guid>
{
    public Guid VehicleId { get; set; }
    public Guid SellerId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ListingStatus Status { get; set; } = ListingStatus.Draft;

    public ProfileType AdvertiserType { get; set; } = ProfileType.Personal;
    public string? BusinessName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? WhatsAppNumber { get; set; }
    public string? ContactHours { get; set; }

    public int DepartmentId { get; set; }
    public int ProvinceId { get; set; }
    public int DistrictId { get; set; }
    public string? LocationReference { get; set; }
    public string? AdditionalConditions { get; set; }

    public bool IsFeatured { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public Guid? ReviewedById { get; set; }
    public string? RejectionReason { get; set; }
    /// <summary>Modalidad reservada, alquilada o vendida cuando el estado es RESERVED/RENTED/SOLD.</summary>
    public Guid? CommittedOperationId { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public Vehicle Vehicle { get; set; } = null!;
    public ApplicationUser Seller { get; set; } = null!;
    public Department Department { get; set; } = null!;
    public Province Province { get; set; } = null!;
    public District District { get; set; } = null!;
    public ICollection<ListingOperation> Operations { get; set; } = new List<ListingOperation>();
}

/// <summary>Modalidad comercial de una publicación con sus precios, condiciones y disponibilidad.</summary>
public class ListingOperation : AuditableEntity<Guid>
{
    public Guid ListingId { get; set; }
    public OperationType OperationType { get; set; }
    public OperationStatus Status { get; set; } = OperationStatus.Available;
    public bool IsActive { get; set; } = true;
    public Currency Currency { get; set; } = Currency.Pen;
    public decimal? SalePrice { get; set; }
    public decimal? DailyRate { get; set; }
    public decimal? WeeklyRate { get; set; }
    public decimal? MonthlyRate { get; set; }
    /// <summary>Precio usado para filtrar/ordenar: venta = precio; alquiler = tarifa diaria (o equivalente diario).</summary>
    public decimal ReferencePrice { get; set; }
    public decimal? Guarantee { get; set; }
    public bool IsNegotiable { get; set; }
    public DateTime? AvailableFrom { get; set; }
    public string? AdditionalConditions { get; set; }

    public Listing Listing { get; set; } = null!;
    public WorkRentCondition? WorkRent { get; set; }
}

/// <summary>Condiciones DECLARADAS por el propietario para WORK_RENT. No se valida legalmente al conductor.</summary>
public class WorkRentCondition
{
    public Guid ListingOperationId { get; set; }
    public WorkUse AllowedUse { get; set; }
    public WorkShift Shift { get; set; }
    public int MinExperienceYears { get; set; }
    public string? LicenseType { get; set; }
    public string? WorkZone { get; set; }
    public int? MonthlyKmLimit { get; set; }
    public CostBearer FuelPaidBy { get; set; }
    public CostBearer MaintenancePaidBy { get; set; }
    public CostBearer InsurancePaidBy { get; set; }
    public string? DriverRequirements { get; set; }
    public ListingOperation ListingOperation { get; set; } = null!;
}

public class ListingStatusHistory
{
    public long Id { get; set; }
    public Guid ListingId { get; set; }
    public ListingStatus? FromStatus { get; set; }
    public ListingStatus ToStatus { get; set; }
    public OperationType? OperationType { get; set; }
    public Guid? ChangedById { get; set; }
    public DateTime ChangedAt { get; set; }
    public string? Note { get; set; }
}

public class Favorite
{
    public Guid UserId { get; set; }
    public Guid ListingId { get; set; }
    public DateTime CreatedAt { get; set; }
    public ApplicationUser User { get; set; } = null!;
    public Listing Listing { get; set; } = null!;
}

public class Negotiation : AuditableEntity<Guid>
{
    public Guid ListingId { get; set; }
    public Guid ListingOperationId { get; set; }
    public Guid BuyerId { get; set; }
    public Guid SellerId { get; set; }
    public NegotiationStatus Status { get; set; } = NegotiationStatus.Pending;
    public Currency Currency { get; set; }
    /// <summary>Periodicidad de la tarifa (null para ventas).</summary>
    public RentPeriod? Periodicity { get; set; }
    public decimal InitialAmount { get; set; }
    public DateTime ExpiresAt { get; set; }
    public int LastOfferSequence { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public Listing Listing { get; set; } = null!;
    public ListingOperation ListingOperation { get; set; } = null!;
    public ApplicationUser Buyer { get; set; } = null!;
    public ApplicationUser Seller { get; set; } = null!;
    public ICollection<NegotiationOffer> Offers { get; set; } = new List<NegotiationOffer>();
}

/// <summary>Cada oferta/contrapropuesta es un registro independiente e inmutable (historial).</summary>
public class NegotiationOffer : AuditableEntity<Guid>
{
    public Guid NegotiationId { get; set; }
    public int Sequence { get; set; }
    public Guid OfferedById { get; set; }
    public decimal Amount { get; set; }
    public Currency Currency { get; set; }
    public RentPeriod? Periodicity { get; set; }
    public string? Message { get; set; }

    public Negotiation Negotiation { get; set; } = null!;
    public ApplicationUser OfferedBy { get; set; } = null!;
}

public class Subscription : AuditableEntity<Guid>
{
    public Guid UserId { get; set; }
    public int PlanId { get; set; }
    public DateTime StartsAt { get; set; }
    /// <summary>Null en el plan gratuito (sin vencimiento).</summary>
    public DateTime? EndsAt { get; set; }
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;
    public bool AutoRenew { get; set; }
    public DateTime? CancelledAt { get; set; }
    public PaymentMode PaymentMode { get; set; } = PaymentMode.None;
    public Guid? CreatedByAdminId { get; set; }

    public ApplicationUser User { get; set; } = null!;
    public Plan Plan { get; set; } = null!;
}

public class Payment : AuditableEntity<Guid>
{
    public Guid UserId { get; set; }
    public Guid? SubscriptionId { get; set; }
    public decimal Amount { get; set; }
    public Currency Currency { get; set; } = Currency.Pen;
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    /// <summary>true = pago de demostración: no representa dinero real y no se mezcla con ingresos reales.</summary>
    public bool IsSimulated { get; set; }
    public string? Provider { get; set; }
    public string? ExternalReference { get; set; }
    public DateTime? PaidAt { get; set; }

    public ApplicationUser User { get; set; } = null!;
    public Subscription? Subscription { get; set; }
}

public class Notification : AuditableEntity<Guid>
{
    public Guid UserId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Guid? RelatedEntityId { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public ApplicationUser User { get; set; } = null!;
}

public class ListingView
{
    public long Id { get; set; }
    public Guid ListingId { get; set; }
    public Guid? ViewerId { get; set; }
    public DateTime ViewedAt { get; set; }
    public Listing Listing { get; set; } = null!;
}

/// <summary>Clic de contacto registrado (intención). No equivale a un contacto comercial concretado.</summary>
public class ContactEvent
{
    public long Id { get; set; }
    public Guid ListingId { get; set; }
    public Guid? UserId { get; set; }
    public ContactChannel Channel { get; set; }
    public DateTime CreatedAt { get; set; }
    public Listing Listing { get; set; } = null!;
}

public class AuditLog
{
    public long Id { get; set; }
    public Guid? UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? EntityName { get; set; }
    public string? EntityId { get; set; }
    public string? Details { get; set; }
    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; }
}
