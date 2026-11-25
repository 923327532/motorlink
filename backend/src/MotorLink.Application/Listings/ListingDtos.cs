using System.ComponentModel.DataAnnotations;
using MotorLink.Application.Vehicles;
using MotorLink.Domain.Common;

namespace MotorLink.Application.Listings;

public sealed record WorkRentRequest
{
    public WorkUse AllowedUse { get; init; }
    public WorkShift Shift { get; init; }
    [Range(0, 60)] public int MinExperienceYears { get; init; }
    [StringLength(30)] public string? LicenseType { get; init; }
    [StringLength(200)] public string? WorkZone { get; init; }
    [Range(1, 100_000)] public int? MonthlyKmLimit { get; init; }
    public CostBearer FuelPaidBy { get; init; }
    public CostBearer MaintenancePaidBy { get; init; }
    public CostBearer InsurancePaidBy { get; init; }
    [StringLength(1000)] public string? DriverRequirements { get; init; }
}

public sealed record ListingOperationRequest
{
    public OperationType OperationType { get; init; }
    public Currency Currency { get; init; } = Currency.Pen;
    [Range(0.01, 100_000_000)] public decimal? SalePrice { get; init; }
    [Range(0.01, 1_000_000)] public decimal? DailyRate { get; init; }
    [Range(0.01, 10_000_000)] public decimal? WeeklyRate { get; init; }
    [Range(0.01, 100_000_000)] public decimal? MonthlyRate { get; init; }
    [Range(0, 100_000_000)] public decimal? Guarantee { get; init; }
    public bool IsNegotiable { get; init; }
    public DateTime? AvailableFrom { get; init; }
    [StringLength(1000)] public string? AdditionalConditions { get; init; }
    /// <summary>Obligatorio y exclusivo para WORK_RENT.</summary>
    public WorkRentRequest? WorkRent { get; init; }
}

public sealed record ListingRequest
{
    public Guid VehicleId { get; init; }
    [Required, StringLength(150, MinimumLength = 5)] public string Title { get; init; } = string.Empty;
    [StringLength(4000)] public string? Description { get; init; }
    public ProfileType AdvertiserType { get; init; } = ProfileType.Personal;
    [StringLength(200)] public string? BusinessName { get; init; }
    [StringLength(20)] public string? PhoneNumber { get; init; }
    [StringLength(20)] public string? WhatsAppNumber { get; init; }
    [StringLength(100)] public string? ContactHours { get; init; }
    [Range(1, int.MaxValue)] public int DepartmentId { get; init; }
    [Range(1, int.MaxValue)] public int ProvinceId { get; init; }
    [Range(1, int.MaxValue)] public int DistrictId { get; init; }
    [StringLength(300)] public string? LocationReference { get; init; }
    [StringLength(1000)] public string? AdditionalConditions { get; init; }
    [Required, MinLength(1), MaxLength(3)] public List<ListingOperationRequest> Operations { get; init; } = [];
}

public sealed record ChangeListingStatusRequest
{
    public ListingStatus Status { get; init; }
    /// <summary>Modalidad afectada; obligatoria para RESERVED y RENTED.</summary>
    public OperationType? OperationType { get; init; }
}

public sealed record SetFeaturedRequest(bool IsFeatured);
public sealed record RejectListingRequest([Required, StringLength(500, MinimumLength = 5)] string Reason);

public sealed record WorkRentDto(
    WorkUse AllowedUse, WorkShift Shift, int MinExperienceYears, string? LicenseType, string? WorkZone, int? MonthlyKmLimit,
    CostBearer FuelPaidBy, CostBearer MaintenancePaidBy, CostBearer InsurancePaidBy, string? DriverRequirements);

public sealed record OperationDto(
    Guid Id, OperationType OperationType, OperationStatus Status, bool IsActive, Currency Currency, decimal? SalePrice,
    decimal? DailyRate, decimal? WeeklyRate, decimal? MonthlyRate, decimal? Guarantee, bool IsNegotiable, DateTime? AvailableFrom,
    string? AdditionalConditions, WorkRentDto? WorkRent);

public sealed record ListingDto(
    Guid Id, Guid VehicleId, string Title, string? Description, ListingStatus Status, bool IsFeatured, ProfileType AdvertiserType,
    string? BusinessName, string? PhoneNumber, string? WhatsAppNumber, string? ContactHours, int DepartmentId, int ProvinceId,
    int DistrictId, string? LocationReference, string? AdditionalConditions, string? RejectionReason, DateTime? PublishedAt,
    DateTime CreatedAt, DateTime? UpdatedAt, string VehicleSummary, string? CoverUrl, int PhotoCount, IReadOnlyList<OperationDto> Operations);

// ---------- Público ----------
public sealed record PublicOperationSummaryDto(
    OperationType OperationType, OperationStatus Status, Currency Currency, decimal? SalePrice, decimal? DailyRate,
    decimal? WeeklyRate, decimal? MonthlyRate, bool IsNegotiable);

public sealed record ListingSummaryDto(
    Guid Id, string Title, bool IsAvailable, ListingStatus Status, bool IsFeatured, string? CoverUrl, string VehicleType,
    string Brand, string Model, string? Version, int ModelYear, int Mileage, VehicleCondition Condition, FuelType FuelType,
    TransmissionType Transmission, string Department, string Province, string District, DateTime? PublishedAt,
    IReadOnlyList<PublicOperationSummaryDto> Operations);

public sealed record PublicAdvertiserDto(Guid UserId, string DisplayName, ProfileType AdvertiserType, DateTime MemberSince, int ActiveListings);

public sealed record PublicVehicleDto(
    string VehicleType, string Brand, string Model, string? Version, int ManufactureYear, int ModelYear, string? MaskedPlate,
    string? Color, int Mileage, VehicleCondition Condition, int PreviousOwners, FuelType FuelType, TransmissionType Transmission,
    int? EngineDisplacementCc, int? Doors, int? Seats, TractionType? Traction, int? LoadCapacityKg, bool HasAirConditioning,
    bool HasGps, bool HasInsurance, bool HasValidSoat, bool HasValidTechnicalReview, string? Description);

public sealed record ListingDetailDto(
    Guid Id, string Title, string? Description, bool IsAvailable, ListingStatus Status, bool IsFeatured, string Department,
    string Province, string District, string? LocationReference, string? AdditionalConditions, string? ContactHours,
    DateTime? PublishedAt, PublicVehicleDto Vehicle, IReadOnlyList<VehicleImageDto> Images,
    IReadOnlyList<OperationDto> Operations, PublicAdvertiserDto Advertiser, bool ContactAvailable);

public sealed record ContactRequest(ContactChannel Channel);
public sealed record ContactInfoDto(ContactChannel Channel, string? PhoneNumber, string? WhatsAppUrl, string? ContactHours, string Disclaimer);

public enum ListingSort { Newest = 1, PriceAsc = 2, PriceDesc = 3, YearDesc = 4, YearAsc = 5 }
public enum ListingAvailability { Available = 1, Reserved = 2, Rented = 3, All = 4 }

public sealed record ListingSearchQuery
{
    public string? Q { get; init; }
    public OperationType? Operation { get; init; }
    public int? VehicleTypeId { get; init; }
    public int? BrandId { get; init; }
    public int? ModelId { get; init; }
    public int? YearMin { get; init; }
    public int? YearMax { get; init; }
    public decimal? PriceMin { get; init; }
    public decimal? PriceMax { get; init; }
    /// <summary>Obligatoria si se filtra u ordena por precio: nunca se comparan monedas distintas.</summary>
    public Currency? Currency { get; init; }
    public VehicleCondition? Condition { get; init; }
    public int? MaxMileage { get; init; }
    public FuelType? Fuel { get; init; }
    public TransmissionType? Transmission { get; init; }
    public int? DepartmentId { get; init; }
    public int? ProvinceId { get; init; }
    public int? DistrictId { get; init; }
    public ListingAvailability Availability { get; init; } = ListingAvailability.Available;
    /// <summary>Solo modalidades disponibles a partir de esta fecha (o sin fecha).</summary>
    public DateTime? AvailableBy { get; init; }
    public bool? Negotiable { get; init; }
    public WorkUse? WorkUse { get; init; }
    public ListingSort Sort { get; init; } = ListingSort.Newest;
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
