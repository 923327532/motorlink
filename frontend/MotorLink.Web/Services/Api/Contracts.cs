namespace MotorLink.Web.Services.Api;

// ============================================================================
// Contratos HTTP de la API. El frontend NO referencia proyectos del backend:
// solo conoce el JSON público. Los enums viajan como texto en MAYÚSCULAS
// (PENDING_REVIEW, WORK_RENT...) y aquí se mantienen como string.
// ============================================================================

public sealed record ApiError(int Status, string Title, string? Detail, IReadOnlyDictionary<string, string[]> FieldErrors, string? Code = null)
{
    public string Message => Detail ?? Title;
}

public sealed record ApiResult<T>(T? Value, ApiError? Error)
{
    public bool Success => Error is null;
    public static ApiResult<T> Ok(T value) => new(value, null);
    public static ApiResult<T> Fail(ApiError error) => new(default, error);
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount, int TotalPages);

// ---- Autenticación / perfil ----
public sealed record ApiUser(Guid Id, string Email, string FullName, string[] Roles);
public sealed record ApiAuthResponse(string AccessToken, DateTime AccessTokenExpiresAt, string RefreshToken, DateTime RefreshTokenExpiresAt, ApiUser User);
public sealed record ApiBusinessProfile(string BusinessName, string? Ruc, string? Description, string? Website, string? PhoneNumber, int? DistrictId, string? Address, bool IsVerified);
public sealed record ApiProfile(Guid UserId, string Email, string ProfileType, string FirstName, string LastName, string? PhoneNumber, string? DocumentNumber, int? DistrictId, string? Address, ApiBusinessProfile? Business);

// ---- Catálogos ----
public sealed record VehicleTypeDto(int Id, string Code, string Name, bool IsActive);
public sealed record BrandDto(int Id, string Name, bool IsActive);
public sealed record ModelDto(int Id, int BrandId, string Name, IReadOnlyList<int> VehicleTypeIds, bool IsActive);
public sealed record DepartmentDto(int Id, string Code, string Name, bool IsActive);
public sealed record ProvinceDto(int Id, int DepartmentId, string Code, string Name, bool IsActive);
public sealed record DistrictDto(int Id, int ProvinceId, string Code, string Name, bool IsActive);

// ---- Publicaciones (público) ----
public sealed record OperationSummary(string OperationType, string Status, string Currency, decimal? SalePrice, decimal? DailyRate, decimal? WeeklyRate, decimal? MonthlyRate, bool IsNegotiable);

public sealed record ListingSummary(
    Guid Id, string Title, bool IsAvailable, string Status, bool IsFeatured, string? CoverUrl, string VehicleType, string Brand, string Model,
    string? Version, int ModelYear, int Mileage, string Condition, string FuelType, string Transmission, string Department, string Province,
    string District, DateTime? PublishedAt, IReadOnlyList<OperationSummary> Operations);

public sealed record WorkRentInfo(string AllowedUse, string Shift, int MinExperienceYears, string? LicenseType, string? WorkZone, int? MonthlyKmLimit,
    string FuelPaidBy, string MaintenancePaidBy, string InsurancePaidBy, string? DriverRequirements);

public sealed record OperationInfo(Guid Id, string OperationType, string Status, bool IsActive, string Currency, decimal? SalePrice, decimal? DailyRate,
    decimal? WeeklyRate, decimal? MonthlyRate, decimal? Guarantee, bool IsNegotiable, DateTime? AvailableFrom, string? AdditionalConditions, WorkRentInfo? WorkRent);

public sealed record ImageInfo(Guid Id, string Url, int SortOrder, bool IsCover);

public sealed record PublicVehicle(
    string VehicleType, string Brand, string Model, string? Version, int ManufactureYear, int ModelYear, string? MaskedPlate, string? Color, int Mileage,
    string Condition, int PreviousOwners, string FuelType, string Transmission, int? EngineDisplacementCc, int? Doors, int? Seats, string? Traction,
    int? LoadCapacityKg, bool HasAirConditioning, bool HasGps, bool HasInsurance, bool HasValidSoat, bool HasValidTechnicalReview, string? Description);

public sealed record Advertiser(Guid UserId, string DisplayName, string AdvertiserType, DateTime MemberSince, int ActiveListings);

public sealed record ListingDetail(
    Guid Id, string Title, string? Description, bool IsAvailable, string Status, bool IsFeatured, string Department, string Province, string District,
    string? LocationReference, string? AdditionalConditions, string? ContactHours, DateTime? PublishedAt, PublicVehicle Vehicle, IReadOnlyList<ImageInfo> Images,
    IReadOnlyList<OperationInfo> Operations, Advertiser Advertiser, bool ContactAvailable);

public sealed record ContactInfo(string Channel, string? PhoneNumber, string? WhatsAppUrl, string? ContactHours, string Disclaimer);

// ---- Publicaciones y vehículos propios ----
public sealed record MyListing(
    Guid Id, Guid VehicleId, string Title, string? Description, string Status, bool IsFeatured, string AdvertiserType, string? BusinessName,
    string? PhoneNumber, string? WhatsAppNumber, string? ContactHours, int DepartmentId, int ProvinceId, int DistrictId, string? LocationReference,
    string? AdditionalConditions, string? RejectionReason, DateTime? PublishedAt, DateTime CreatedAt, DateTime? UpdatedAt, string VehicleSummary,
    string? CoverUrl, int PhotoCount, IReadOnlyList<OperationInfo> Operations);

public sealed record MyVehicle(
    Guid Id, Guid OwnerId, int VehicleTypeId, string VehicleTypeName, int BrandId, string BrandName, int ModelId, string ModelName, string? Version,
    int ManufactureYear, int ModelYear, string? Plate, string? Color, int Mileage, string Condition, int PreviousOwners, string FuelType, string Transmission,
    int? EngineDisplacementCc, int? Doors, int? Seats, string? Traction, int? LoadCapacityKg, bool HasAirConditioning, bool HasGps, bool HasInsurance,
    bool HasValidSoat, bool HasValidTechnicalReview, string? Description, string? LegalOwnerName, string? LegalOwnerRelationship, bool OwnershipVerified,
    DateTime CreatedAt, DateTime? UpdatedAt, IReadOnlyList<ImageInfo> Images);

// ---- Negociaciones ----
public sealed record NegotiationSummary(
    Guid Id, Guid ListingId, string ListingTitle, string OperationType, Guid BuyerId, string BuyerName, Guid SellerId, string SellerName, string Status,
    string Currency, string? Periodicity, decimal InitialAmount, decimal LastAmount, Guid LastOfferById, DateTime CreatedAt, DateTime? UpdatedAt, DateTime ExpiresAt);

public sealed record OfferInfo(Guid Id, int Sequence, Guid OfferedById, decimal Amount, string Currency, string? Periodicity, string? Message, DateTime CreatedAt);
public sealed record NegotiationDetail(NegotiationSummary Negotiation, decimal? ListedPrice, decimal? AgreedAmount, bool AwaitingMyResponse, IReadOnlyList<OfferInfo> Offers);

// ---- Planes y suscripciones ----
public sealed record PlanInfo(int Id, string Code, string Name, decimal MonthlyPrice, int MaxActiveListings, int MaxPhotosPerVehicle, int MaxFeaturedListings,
    bool HasAdvancedStats, bool HasBusinessProfile, bool CanExportReports, bool HasPrioritySupport, int SortOrder, bool IsActive);

public sealed record SubscriptionInfo(Guid Id, Guid UserId, string PlanCode, string PlanName, decimal MonthlyPrice, string Status, DateTime StartsAt, DateTime? EndsAt,
    bool AutoRenew, DateTime? CancelledAt, string PaymentMode, DateTime CreatedAt);

public sealed record PaymentInfo(Guid Id, Guid UserId, Guid? SubscriptionId, decimal Amount, string Currency, string Status, bool IsSimulated, string? Provider,
    string? ExternalReference, DateTime CreatedAt, DateTime? PaidAt);

public sealed record PlanUsage(int ActiveListings, int MaxActiveListings, int FeaturedListings, int MaxFeaturedListings, int MaxPhotosPerVehicle);
public sealed record CurrentPlan(SubscriptionInfo Subscription, PlanInfo Plan, PlanUsage Usage);

// ---- Dashboard y notificaciones ----
public sealed record MonthlyPoint(string Month, int Views, int ContactClicks, int ProposalsReceived, int VehiclesSold, int VehiclesRented);

public sealed record DashboardData(
    DateTime From, DateTime To, string PlanCode, PlanUsage PlanUsage, int ActiveListings, int VehiclesRegistered, int Views, int ContactClicks, int ProposalsReceived,
    int ProposalsSent, int AcceptedNegotiations, int VehiclesSold, int VehiclesRented, double ConversionRate, double ContactRate, bool AdvancedStats,
    IReadOnlyList<MonthlyPoint>? MonthlyEvolution);

public sealed record NotificationInfo(Guid Id, string Type, string Title, string Message, Guid? RelatedEntityId, bool IsRead, DateTime CreatedAt);
public sealed record UnreadCount(int Count);

// ---- Administración ----
public sealed record AdminUser(Guid Id, string Email, string FullName, IReadOnlyList<string> Roles, bool IsActive, DateTime CreatedAt, int Listings, string? PlanCode);
public sealed record ModerationItem(Guid Id, string Title, string Status, Guid SellerId, string SellerEmail, string Vehicle, DateTime CreatedAt, DateTime? ReviewedAt, string? RejectionReason);
public sealed record AuditLogInfo(long Id, Guid? UserId, string Action, string? EntityName, string? EntityId, string? Details, string? IpAddress, DateTime CreatedAt);
public sealed record PlanDistribution(string PlanCode, string PlanName, int ActiveSubscribers);
public sealed record CategoryCount(string Category, int Listings);
public sealed record MonthlyGrowth(string Month, int NewUsers, int NewListings);
public sealed record AdminStats(int UsersRegistered, int UsersActive, int PayingSubscribers, IReadOnlyList<PlanDistribution> PlanDistribution,
    IReadOnlyList<CategoryCount> ListingsByCategory, int VehiclesSold, int VehiclesRented, IReadOnlyList<MonthlyGrowth> MonthlyGrowth,
    decimal MrrReal, decimal MrrSimulated, decimal ComplimentaryValue);
public sealed record PlanRevenueRow(string PlanCode, string PlanName, decimal MonthlyPrice, int ActiveTotal, int Real, int Simulated, int Complimentary, int Unpaid,
    decimal MonthlyValueReal, decimal MonthlyValueSimulated, decimal MonthlyValueComplimentary);
public sealed record PlanRevenueReport(DateTime GeneratedAt, IReadOnlyList<PlanRevenueRow> Plans, decimal TotalReal, decimal TotalSimulated, decimal TotalComplimentary, int TotalActiveSubscriptions);
public sealed record UsersByProfileTypeRow(string ProfileType, int TotalUsers);
public sealed record AdminBusiness(Guid Id, Guid UserId, string BusinessName, string? Ruc, string OwnerEmail, bool IsVerified, int ActiveListings, DateTime CreatedAt);
public sealed record AdminVehicle(Guid Id, string OwnerEmail, string VehicleType, string Brand, string Model, int ModelYear, string? MaskedPlate, string Condition, int Mileage,
    string? ListingStatus, int Photos, DateTime CreatedAt);
public sealed record AdminNegotiation(Guid Id, string ListingTitle, string OperationType, string BuyerEmail, string SellerEmail, string Status, string Currency,
    string? Periodicity, decimal InitialAmount, decimal LastAmount, DateTime CreatedAt, DateTime? UpdatedAt);
