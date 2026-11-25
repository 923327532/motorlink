namespace MotorLink.Domain.Common;

public enum ProfileType { Personal = 1, Business = 2 }
public enum VehicleCondition { New = 1, SemiNew = 2, Used = 3 }
public enum FuelType { Gasoline = 1, Diesel = 2, Lpg = 3, Cng = 4, Hybrid = 5, Electric = 6 }
public enum TransmissionType { Manual = 1, Automatic = 2 }
public enum TractionType { Fwd = 1, Rwd = 2, Awd = 3, FourByFour = 4 }
public enum Currency { Pen = 1, Usd = 2 }

/// <summary>Estado de una publicación (JSON: DRAFT, PENDING_REVIEW, PUBLISHED, ...).</summary>
public enum ListingStatus
{
    Draft = 1, PendingReview = 2, Published = 3, Reserved = 4, Sold = 5, Rented = 6, Paused = 7, Rejected = 8, Archived = 9
}

/// <summary>Modalidad comercial (JSON: SALE, RENT, WORK_RENT).</summary>
public enum OperationType { Sale = 1, Rent = 2, WorkRent = 3 }
public enum OperationStatus { Available = 1, Reserved = 2, Rented = 3, Sold = 4, Closed = 5 }
public enum RentPeriod { Daily = 1, Weekly = 2, Monthly = 3 }

public enum WorkUse { Taxi = 1, Delivery = 2, Transport = 3, Cargo = 4 }
public enum WorkShift { Day = 1, Night = 2, Full = 3 }
public enum CostBearer { Owner = 1, Driver = 2, Shared = 3 }

public enum NegotiationStatus { Pending = 1, CounterOffer = 2, Accepted = 3, Rejected = 4, Cancelled = 5, Expired = 6 }

public enum SubscriptionStatus { Active = 1, Pending = 2, Expired = 3, Cancelled = 4 }
/// <summary>Cómo se activó la suscripción; separa ingresos reales de simulados.</summary>
public enum PaymentMode { None = 0, Simulated = 1, Real = 2, Complimentary = 3 }
public enum PaymentStatus { Pending = 1, Paid = 2, Failed = 3, Refunded = 4 }

public enum ContactChannel { Phone = 1, WhatsApp = 2, Email = 3, Chat = 4 }
public enum RefreshTokenRevocationReason { Rotated = 1, Logout = 2, ReuseDetected = 3, PasswordChanged = 4, Admin = 5 }
