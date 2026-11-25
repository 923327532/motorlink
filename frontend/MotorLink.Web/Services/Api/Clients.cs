using Microsoft.Extensions.Caching.Memory;

namespace MotorLink.Web.Services.Api;

// ---------------------------------------------------------------- autenticación / perfil
public interface IAuthApi
{
    Task<ApiResult<ApiAuthResponse>> LoginAsync(string email, string password, CancellationToken ct);
    Task<ApiResult<ApiAuthResponse>> RegisterAsync(object body, CancellationToken ct);
    Task<ApiResult<bool>> LogoutAsync(string refreshToken, CancellationToken ct);
    Task<ApiResult<bool>> ChangePasswordAsync(string current, string next, CancellationToken ct);
}

public sealed class AuthApiClient(HttpClient http) : ApiClientBase(http), IAuthApi
{
    public Task<ApiResult<ApiAuthResponse>> LoginAsync(string email, string password, CancellationToken ct) =>
        PostAsync<ApiAuthResponse>("api/v1/auth/login", new { email, password }, ct, authErrors: false);
    public Task<ApiResult<ApiAuthResponse>> RegisterAsync(object body, CancellationToken ct) => PostAsync<ApiAuthResponse>("api/v1/auth/register", body, ct, authErrors: false);
    public Task<ApiResult<bool>> LogoutAsync(string refreshToken, CancellationToken ct) => PostAsync<bool>("api/v1/auth/logout", new { refreshToken }, ct, authErrors: false);
    public Task<ApiResult<bool>> ChangePasswordAsync(string current, string next, CancellationToken ct) =>
        PostAsync<bool>("api/v1/auth/change-password", new { currentPassword = current, newPassword = next }, ct);
}

public interface IUsersApi
{
    Task<ApiResult<ApiProfile>> GetProfileAsync(CancellationToken ct);
    Task<ApiResult<ApiProfile>> UpdateProfileAsync(object body, CancellationToken ct);
}

public sealed class UsersApiClient(HttpClient http) : ApiClientBase(http), IUsersApi
{
    public Task<ApiResult<ApiProfile>> GetProfileAsync(CancellationToken ct) => GetAsync<ApiProfile>("api/v1/users/profile", ct);
    public Task<ApiResult<ApiProfile>> UpdateProfileAsync(object body, CancellationToken ct) => PutAsync<ApiProfile>("api/v1/users/profile", body, ct);
}

// ---------------------------------------------------------------- catálogos (públicos, con caché breve)
public interface ICatalogApi
{
    Task<IReadOnlyList<VehicleTypeDto>> VehicleTypesAsync(CancellationToken ct);
    Task<IReadOnlyList<BrandDto>> BrandsAsync(CancellationToken ct);
    Task<IReadOnlyList<ModelDto>> ModelsAsync(int? brandId, int? vehicleTypeId, CancellationToken ct);
    Task<IReadOnlyList<DepartmentDto>> DepartmentsAsync(CancellationToken ct);
    Task<IReadOnlyList<ProvinceDto>> ProvincesAsync(int? departmentId, CancellationToken ct);
    Task<IReadOnlyList<DistrictDto>> DistrictsAsync(int? provinceId, CancellationToken ct);
}

public sealed class CatalogApiClient(HttpClient http, IMemoryCache cache) : ApiClientBase(http), ICatalogApi
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

    private async Task<IReadOnlyList<T>> Cached<T>(string key, string url, CancellationToken ct)
    {
        if (cache.TryGetValue(key, out IReadOnlyList<T>? hit) && hit is not null) return hit;
        var r = await GetAsync<List<T>>(url, ct);
        if (!r.Success) return [];                   // un catálogo caído no debe tumbar toda la página
        cache.Set(key, (IReadOnlyList<T>)r.Value!, Ttl);
        return r.Value!;
    }

    public Task<IReadOnlyList<VehicleTypeDto>> VehicleTypesAsync(CancellationToken ct) => Cached<VehicleTypeDto>("cat:types", "api/v1/catalogs/vehicle-types", ct);
    public Task<IReadOnlyList<BrandDto>> BrandsAsync(CancellationToken ct) => Cached<BrandDto>("cat:brands", "api/v1/catalogs/brands", ct);
    public Task<IReadOnlyList<ModelDto>> ModelsAsync(int? brandId, int? vehicleTypeId, CancellationToken ct) =>
        Cached<ModelDto>($"cat:models:{brandId}:{vehicleTypeId}", Url("api/v1/catalogs/models", Query(("brandId", brandId), ("vehicleTypeId", vehicleTypeId))), ct);
    public Task<IReadOnlyList<DepartmentDto>> DepartmentsAsync(CancellationToken ct) => Cached<DepartmentDto>("cat:deps", "api/v1/catalogs/departments", ct);
    public Task<IReadOnlyList<ProvinceDto>> ProvincesAsync(int? departmentId, CancellationToken ct) =>
        Cached<ProvinceDto>($"cat:prov:{departmentId}", Url("api/v1/catalogs/provinces", Query(("departmentId", departmentId))), ct);
    public Task<IReadOnlyList<DistrictDto>> DistrictsAsync(int? provinceId, CancellationToken ct) =>
        Cached<DistrictDto>($"cat:dist:{provinceId}", Url("api/v1/catalogs/districts", Query(("provinceId", provinceId))), ct);
}

// ---------------------------------------------------------------- publicaciones, vehículos y favoritos
public interface IListingsApi
{
    Task<ApiResult<PagedResult<ListingSummary>>> SearchAsync(string query, CancellationToken ct);
    Task<ApiResult<ListingDetail>> DetailAsync(Guid id, CancellationToken ct);
    Task<ApiResult<bool>> RegisterViewAsync(Guid id, CancellationToken ct);
    Task<ApiResult<ContactInfo>> ContactAsync(Guid id, string channel, CancellationToken ct);

    Task<ApiResult<PagedResult<MyListing>>> MineAsync(string? status, int page, int pageSize, CancellationToken ct);
    Task<ApiResult<MyListing>> MineByIdAsync(Guid id, CancellationToken ct);
    Task<ApiResult<MyListing>> CreateAsync(object body, CancellationToken ct);
    Task<ApiResult<MyListing>> UpdateAsync(Guid id, object body, CancellationToken ct);
    Task<ApiResult<MyListing>> ChangeStatusAsync(Guid id, string status, string? operationType, CancellationToken ct);
    Task<ApiResult<MyListing>> SetFeaturedAsync(Guid id, bool featured, CancellationToken ct);
    Task<ApiResult<bool>> DeleteAsync(Guid id, CancellationToken ct);

    Task<ApiResult<PagedResult<ListingSummary>>> FavoritesAsync(int page, int pageSize, CancellationToken ct);
    Task<ApiResult<bool>> AddFavoriteAsync(Guid id, CancellationToken ct);
    Task<ApiResult<bool>> RemoveFavoriteAsync(Guid id, CancellationToken ct);
}

public sealed class ListingsApiClient(HttpClient http) : ApiClientBase(http), IListingsApi
{
    public Task<ApiResult<PagedResult<ListingSummary>>> SearchAsync(string query, CancellationToken ct) => GetAsync<PagedResult<ListingSummary>>(Url("api/v1/listings", query), ct);
    public Task<ApiResult<ListingDetail>> DetailAsync(Guid id, CancellationToken ct) => GetAsync<ListingDetail>($"api/v1/listings/{id}", ct);
    public Task<ApiResult<bool>> RegisterViewAsync(Guid id, CancellationToken ct) => PostAsync<bool>($"api/v1/listings/{id}/views", null, ct, authErrors: false);
    public Task<ApiResult<ContactInfo>> ContactAsync(Guid id, string channel, CancellationToken ct) => PostAsync<ContactInfo>($"api/v1/listings/{id}/contact", new { channel }, ct, authErrors: false);

    public Task<ApiResult<PagedResult<MyListing>>> MineAsync(string? status, int page, int pageSize, CancellationToken ct) =>
        GetAsync<PagedResult<MyListing>>(Url("api/v1/listings/mine", Query(("status", status), ("page", page), ("pageSize", pageSize))), ct);
    public Task<ApiResult<MyListing>> MineByIdAsync(Guid id, CancellationToken ct) => GetAsync<MyListing>($"api/v1/listings/mine/{id}", ct);
    public Task<ApiResult<MyListing>> CreateAsync(object body, CancellationToken ct) => PostAsync<MyListing>("api/v1/listings", body, ct);
    public Task<ApiResult<MyListing>> UpdateAsync(Guid id, object body, CancellationToken ct) => PutAsync<MyListing>($"api/v1/listings/{id}", body, ct);
    public Task<ApiResult<MyListing>> ChangeStatusAsync(Guid id, string status, string? operationType, CancellationToken ct) =>
        PatchAsync<MyListing>($"api/v1/listings/{id}/status", new { status, operationType }, ct);
    public Task<ApiResult<MyListing>> SetFeaturedAsync(Guid id, bool featured, CancellationToken ct) => PatchAsync<MyListing>($"api/v1/listings/{id}/featured", new { isFeatured = featured }, ct);
    public Task<ApiResult<bool>> DeleteAsync(Guid id, CancellationToken ct) => base.DeleteAsync($"api/v1/listings/{id}", ct);

    public Task<ApiResult<PagedResult<ListingSummary>>> FavoritesAsync(int page, int pageSize, CancellationToken ct) =>
        GetAsync<PagedResult<ListingSummary>>(Url("api/v1/favorites", Query(("page", page), ("pageSize", pageSize))), ct);
    public Task<ApiResult<bool>> AddFavoriteAsync(Guid id, CancellationToken ct) => SendAsync<bool>(HttpMethod.Put, $"api/v1/favorites/{id}", null, ct);
    public Task<ApiResult<bool>> RemoveFavoriteAsync(Guid id, CancellationToken ct) => base.DeleteAsync($"api/v1/favorites/{id}", ct);
}

public interface IVehiclesApi
{
    Task<ApiResult<PagedResult<MyVehicle>>> ListAsync(int page, int pageSize, CancellationToken ct);
    Task<ApiResult<MyVehicle>> GetAsync(Guid id, CancellationToken ct);
    Task<ApiResult<MyVehicle>> CreateAsync(object body, CancellationToken ct);
    Task<ApiResult<MyVehicle>> UpdateAsync(Guid id, object body, CancellationToken ct);
    Task<ApiResult<bool>> DeleteAsync(Guid id, CancellationToken ct);
    Task<ApiResult<IReadOnlyList<ImageInfo>>> ImagesAsync(Guid id, CancellationToken ct);
    Task<ApiResult<ImageInfo>> UploadImageAsync(Guid id, Stream content, string fileName, string contentType, CancellationToken ct);
    Task<ApiResult<bool>> DeleteImageAsync(Guid id, Guid imageId, CancellationToken ct);
    Task<ApiResult<IReadOnlyList<ImageInfo>>> ReorderImagesAsync(Guid id, IReadOnlyList<Guid> ids, CancellationToken ct);
    Task<ApiResult<IReadOnlyList<ImageInfo>>> SetCoverAsync(Guid id, Guid imageId, CancellationToken ct);
}

public sealed class VehiclesApiClient(HttpClient http) : ApiClientBase(http), IVehiclesApi
{
    public Task<ApiResult<PagedResult<MyVehicle>>> ListAsync(int page, int pageSize, CancellationToken ct) =>
        GetAsync<PagedResult<MyVehicle>>(Url("api/v1/vehicles", Query(("page", page), ("pageSize", pageSize))), ct);
    public Task<ApiResult<MyVehicle>> GetAsync(Guid id, CancellationToken ct) => GetAsync<MyVehicle>($"api/v1/vehicles/{id}", ct);
    public Task<ApiResult<MyVehicle>> CreateAsync(object body, CancellationToken ct) => PostAsync<MyVehicle>("api/v1/vehicles", body, ct);
    public Task<ApiResult<MyVehicle>> UpdateAsync(Guid id, object body, CancellationToken ct) => PutAsync<MyVehicle>($"api/v1/vehicles/{id}", body, ct);
    public Task<ApiResult<bool>> DeleteAsync(Guid id, CancellationToken ct) => base.DeleteAsync($"api/v1/vehicles/{id}", ct);
    public async Task<ApiResult<IReadOnlyList<ImageInfo>>> ImagesAsync(Guid id, CancellationToken ct) => Wrap(await GetAsync<List<ImageInfo>>($"api/v1/vehicles/{id}/images", ct));
    public Task<ApiResult<ImageInfo>> UploadImageAsync(Guid id, Stream content, string fileName, string contentType, CancellationToken ct) =>
        UploadAsync<ImageInfo>($"api/v1/vehicles/{id}/images", content, fileName, contentType, ct);
    public Task<ApiResult<bool>> DeleteImageAsync(Guid id, Guid imageId, CancellationToken ct) => base.DeleteAsync($"api/v1/vehicles/{id}/images/{imageId}", ct);
    public async Task<ApiResult<IReadOnlyList<ImageInfo>>> ReorderImagesAsync(Guid id, IReadOnlyList<Guid> ids, CancellationToken ct) =>
        Wrap(await PutAsync<List<ImageInfo>>($"api/v1/vehicles/{id}/images/order", new { imageIds = ids }, ct));
    public async Task<ApiResult<IReadOnlyList<ImageInfo>>> SetCoverAsync(Guid id, Guid imageId, CancellationToken ct) =>
        Wrap(await PatchAsync<List<ImageInfo>>($"api/v1/vehicles/{id}/images/{imageId}/cover", null, ct));

    private static ApiResult<IReadOnlyList<ImageInfo>> Wrap(ApiResult<List<ImageInfo>> r) =>
        r.Success ? ApiResult<IReadOnlyList<ImageInfo>>.Ok(r.Value!) : ApiResult<IReadOnlyList<ImageInfo>>.Fail(r.Error!);
}

// ---------------------------------------------------------------- negociaciones
public interface INegotiationsApi
{
    Task<ApiResult<NegotiationDetail>> StartAsync(object body, CancellationToken ct);
    Task<ApiResult<PagedResult<NegotiationSummary>>> ListAsync(string role, string? status, int page, int pageSize, CancellationToken ct);
    Task<ApiResult<NegotiationDetail>> GetAsync(Guid id, CancellationToken ct);
    Task<ApiResult<NegotiationDetail>> CounterAsync(Guid id, decimal amount, string? message, CancellationToken ct);
    Task<ApiResult<NegotiationDetail>> AcceptAsync(Guid id, Guid offerId, CancellationToken ct);
    Task<ApiResult<NegotiationDetail>> RejectAsync(Guid id, CancellationToken ct);
    Task<ApiResult<NegotiationDetail>> CancelAsync(Guid id, CancellationToken ct);
}

public sealed class NegotiationsApiClient(HttpClient http) : ApiClientBase(http), INegotiationsApi
{
    public Task<ApiResult<NegotiationDetail>> StartAsync(object body, CancellationToken ct) => PostAsync<NegotiationDetail>("api/v1/negotiations", body, ct);
    public Task<ApiResult<PagedResult<NegotiationSummary>>> ListAsync(string role, string? status, int page, int pageSize, CancellationToken ct) =>
        GetAsync<PagedResult<NegotiationSummary>>(Url("api/v1/negotiations", Query(("role", role), ("status", status), ("page", page), ("pageSize", pageSize))), ct);
    public Task<ApiResult<NegotiationDetail>> GetAsync(Guid id, CancellationToken ct) => GetAsync<NegotiationDetail>($"api/v1/negotiations/{id}", ct);
    public Task<ApiResult<NegotiationDetail>> CounterAsync(Guid id, decimal amount, string? message, CancellationToken ct) =>
        PostAsync<NegotiationDetail>($"api/v1/negotiations/{id}/offers", new { amount, message }, ct);
    public Task<ApiResult<NegotiationDetail>> AcceptAsync(Guid id, Guid offerId, CancellationToken ct) => PatchAsync<NegotiationDetail>($"api/v1/negotiations/{id}/accept", new { offerId }, ct);
    public Task<ApiResult<NegotiationDetail>> RejectAsync(Guid id, CancellationToken ct) => PatchAsync<NegotiationDetail>($"api/v1/negotiations/{id}/reject", null, ct);
    public Task<ApiResult<NegotiationDetail>> CancelAsync(Guid id, CancellationToken ct) => PatchAsync<NegotiationDetail>($"api/v1/negotiations/{id}/cancel", null, ct);
}

// ---------------------------------------------------------------- planes, suscripciones, dashboard, notificaciones
public interface IBillingApi
{
    Task<ApiResult<List<PlanInfo>>> PlansAsync(CancellationToken ct);
    Task<ApiResult<CurrentPlan>> CurrentAsync(CancellationToken ct);
    Task<ApiResult<PagedResult<SubscriptionInfo>>> HistoryAsync(int page, int pageSize, CancellationToken ct);
    Task<ApiResult<PagedResult<PaymentInfo>>> PaymentsAsync(int page, int pageSize, CancellationToken ct);
    Task<ApiResult<SubscriptionInfo>> ChangePlanAsync(string planCode, CancellationToken ct);
    Task<ApiResult<SubscriptionInfo>> RenewAsync(CancellationToken ct);
    Task<ApiResult<SubscriptionInfo>> PayAsync(Guid id, CancellationToken ct);
    Task<ApiResult<SubscriptionInfo>> CancelAsync(Guid id, CancellationToken ct);
}

public sealed class BillingApiClient(HttpClient http) : ApiClientBase(http), IBillingApi
{
    public Task<ApiResult<List<PlanInfo>>> PlansAsync(CancellationToken ct) => GetAsync<List<PlanInfo>>("api/v1/plans", ct);
    public Task<ApiResult<CurrentPlan>> CurrentAsync(CancellationToken ct) => GetAsync<CurrentPlan>("api/v1/subscriptions/current", ct);
    public Task<ApiResult<PagedResult<SubscriptionInfo>>> HistoryAsync(int page, int pageSize, CancellationToken ct) =>
        GetAsync<PagedResult<SubscriptionInfo>>(Url("api/v1/subscriptions", Query(("page", page), ("pageSize", pageSize))), ct);
    public Task<ApiResult<PagedResult<PaymentInfo>>> PaymentsAsync(int page, int pageSize, CancellationToken ct) =>
        GetAsync<PagedResult<PaymentInfo>>(Url("api/v1/subscriptions/payments", Query(("page", page), ("pageSize", pageSize))), ct);
    public Task<ApiResult<SubscriptionInfo>> ChangePlanAsync(string planCode, CancellationToken ct) => PostAsync<SubscriptionInfo>("api/v1/subscriptions/change-plan", new { planCode }, ct);
    public Task<ApiResult<SubscriptionInfo>> RenewAsync(CancellationToken ct) => PostAsync<SubscriptionInfo>("api/v1/subscriptions/renew", null, ct);
    public Task<ApiResult<SubscriptionInfo>> PayAsync(Guid id, CancellationToken ct) => PostAsync<SubscriptionInfo>($"api/v1/subscriptions/{id}/pay", null, ct);
    public Task<ApiResult<SubscriptionInfo>> CancelAsync(Guid id, CancellationToken ct) => PatchAsync<SubscriptionInfo>($"api/v1/subscriptions/{id}/cancel", null, ct);
}

public interface IAccountApi
{
    Task<ApiResult<DashboardData>> DashboardAsync(DateTime? from, DateTime? to, CancellationToken ct);
    Task<ApiResult<(byte[] Bytes, string ContentType)>> ExportAsync(DateTime? from, DateTime? to, CancellationToken ct);
    Task<ApiResult<PagedResult<NotificationInfo>>> NotificationsAsync(bool unreadOnly, int page, int pageSize, CancellationToken ct);
    Task<ApiResult<UnreadCount>> UnreadCountAsync(CancellationToken ct);
    Task<ApiResult<bool>> MarkReadAsync(Guid id, CancellationToken ct);
    Task<ApiResult<bool>> MarkAllReadAsync(CancellationToken ct);
}

public sealed class AccountApiClient(HttpClient http) : ApiClientBase(http), IAccountApi
{
    private static string Range(DateTime? from, DateTime? to) => Query(("from", from?.ToString("O")), ("to", to?.ToString("O")));
    public Task<ApiResult<DashboardData>> DashboardAsync(DateTime? from, DateTime? to, CancellationToken ct) => GetAsync<DashboardData>(Url("api/v1/dashboard", Range(from, to)), ct);
    public Task<ApiResult<(byte[] Bytes, string ContentType)>> ExportAsync(DateTime? from, DateTime? to, CancellationToken ct) => DownloadAsync(Url("api/v1/dashboard/export", Range(from, to)), ct);
    public Task<ApiResult<PagedResult<NotificationInfo>>> NotificationsAsync(bool unreadOnly, int page, int pageSize, CancellationToken ct) =>
        GetAsync<PagedResult<NotificationInfo>>(Url("api/v1/notifications", Query(("unreadOnly", unreadOnly ? "true" : null), ("page", page), ("pageSize", pageSize))), ct);
    public Task<ApiResult<UnreadCount>> UnreadCountAsync(CancellationToken ct) => GetAsync<UnreadCount>("api/v1/notifications/unread-count", ct);
    public Task<ApiResult<bool>> MarkReadAsync(Guid id, CancellationToken ct) => PatchAsync<bool>($"api/v1/notifications/{id}/read", null, ct);
    public Task<ApiResult<bool>> MarkAllReadAsync(CancellationToken ct) => PatchAsync<bool>("api/v1/notifications/read-all", null, ct);
}

// ---------------------------------------------------------------- administración
public interface IAdminApi
{
    Task<ApiResult<AdminStats>> StatsAsync(CancellationToken ct);
    Task<ApiResult<PagedResult<AdminUser>>> UsersAsync(string? search, bool? active, int page, int pageSize, CancellationToken ct);
    Task<ApiResult<AdminUser>> SetUserActiveAsync(Guid id, bool active, CancellationToken ct);
    Task<ApiResult<PagedResult<AdminBusiness>>> BusinessesAsync(string? search, int page, int pageSize, CancellationToken ct);
    Task<ApiResult<PagedResult<AdminVehicle>>> VehiclesAsync(string? search, int page, int pageSize, CancellationToken ct);
    Task<ApiResult<PagedResult<ModerationItem>>> ListingsAsync(string? status, int page, int pageSize, CancellationToken ct);
    Task<ApiResult<bool>> ApproveListingAsync(Guid id, CancellationToken ct);
    Task<ApiResult<bool>> RejectListingAsync(Guid id, string reason, CancellationToken ct);
    Task<ApiResult<PagedResult<AdminNegotiation>>> NegotiationsAsync(string? status, int page, int pageSize, CancellationToken ct);
    Task<ApiResult<List<PlanInfo>>> PlansAsync(CancellationToken ct);
    Task<ApiResult<PlanInfo>> UpsertPlanAsync(int? id, object body, CancellationToken ct);
    Task<ApiResult<bool>> SetPlanActiveAsync(int id, bool active, CancellationToken ct);
    Task<ApiResult<bool>> DeletePlanAsync(int id, CancellationToken ct);
    Task<ApiResult<PagedResult<SubscriptionInfo>>> SubscriptionsAsync(string? status, int page, int pageSize, CancellationToken ct);
    Task<ApiResult<SubscriptionInfo>> AssignPlanAsync(Guid userId, string planCode, int months, CancellationToken ct);
    Task<ApiResult<SubscriptionInfo>> ActivateSubscriptionAsync(Guid id, CancellationToken ct);
    Task<ApiResult<SubscriptionInfo>> CancelSubscriptionAsync(Guid id, CancellationToken ct);
    Task<ApiResult<PagedResult<PaymentInfo>>> PaymentsAsync(bool? simulated, int page, int pageSize, CancellationToken ct);
    Task<ApiResult<PaymentInfo>> DemoPaymentAsync(Guid subscriptionId, CancellationToken ct);
    Task<ApiResult<PagedResult<AuditLogInfo>>> AuditAsync(string? action, int page, int pageSize, CancellationToken ct);
    Task<ApiResult<PlanRevenueReport>> PlanRevenueAsync(CancellationToken ct);
    Task<ApiResult<List<UsersByProfileTypeRow>>> UsersByProfileTypeAsync(CancellationToken ct);
    Task<ApiResult<(byte[] Bytes, string ContentType)>> CsvAsync(string report, CancellationToken ct);
    // Catálogos: kind = vehicle-types | brands | models | departments | provinces | districts
    Task<ApiResult<List<T>>> CatalogListAsync<T>(string kind, string? query, CancellationToken ct);
    Task<ApiResult<bool>> CatalogUpsertAsync(string kind, int? id, object body, CancellationToken ct);
    Task<ApiResult<bool>> CatalogActiveAsync(string kind, int id, bool active, CancellationToken ct);
    Task<ApiResult<bool>> CatalogDeleteAsync(string kind, int id, CancellationToken ct);
}

public sealed class AdminApiClient(HttpClient http) : ApiClientBase(http), IAdminApi
{
    private const string A = "api/v1/admin";
    public Task<ApiResult<AdminStats>> StatsAsync(CancellationToken ct) => GetAsync<AdminStats>($"{A}/stats", ct);
    public Task<ApiResult<PagedResult<AdminUser>>> UsersAsync(string? search, bool? active, int page, int pageSize, CancellationToken ct) =>
        GetAsync<PagedResult<AdminUser>>(Url($"{A}/users", Query(("search", search), ("active", active?.ToString().ToLowerInvariant()), ("page", page), ("pageSize", pageSize))), ct);
    public Task<ApiResult<AdminUser>> SetUserActiveAsync(Guid id, bool active, CancellationToken ct) => PatchAsync<AdminUser>($"{A}/users/{id}/status", new { isActive = active }, ct);
    public Task<ApiResult<PagedResult<AdminBusiness>>> BusinessesAsync(string? search, int page, int pageSize, CancellationToken ct) =>
        GetAsync<PagedResult<AdminBusiness>>(Url($"{A}/businesses", Query(("search", search), ("page", page), ("pageSize", pageSize))), ct);
    public Task<ApiResult<PagedResult<AdminVehicle>>> VehiclesAsync(string? search, int page, int pageSize, CancellationToken ct) =>
        GetAsync<PagedResult<AdminVehicle>>(Url($"{A}/vehicles", Query(("search", search), ("page", page), ("pageSize", pageSize))), ct);
    public Task<ApiResult<PagedResult<ModerationItem>>> ListingsAsync(string? status, int page, int pageSize, CancellationToken ct) =>
        GetAsync<PagedResult<ModerationItem>>(Url($"{A}/listings", Query(("status", status), ("page", page), ("pageSize", pageSize))), ct);
    public Task<ApiResult<bool>> ApproveListingAsync(Guid id, CancellationToken ct) => PostAsync<bool>($"{A}/listings/{id}/approve", null, ct);
    public Task<ApiResult<bool>> RejectListingAsync(Guid id, string reason, CancellationToken ct) => PostAsync<bool>($"{A}/listings/{id}/reject", new { reason }, ct);
    public Task<ApiResult<PagedResult<AdminNegotiation>>> NegotiationsAsync(string? status, int page, int pageSize, CancellationToken ct) =>
        GetAsync<PagedResult<AdminNegotiation>>(Url($"{A}/negotiations", Query(("status", status), ("page", page), ("pageSize", pageSize))), ct);
    public Task<ApiResult<List<PlanInfo>>> PlansAsync(CancellationToken ct) => GetAsync<List<PlanInfo>>($"{A}/plans", ct);
    public Task<ApiResult<PlanInfo>> UpsertPlanAsync(int? id, object body, CancellationToken ct) =>
        id is null ? PostAsync<PlanInfo>($"{A}/plans", body, ct) : PutAsync<PlanInfo>($"{A}/plans/{id}", body, ct);
    public Task<ApiResult<bool>> SetPlanActiveAsync(int id, bool active, CancellationToken ct) => PatchAsync<bool>($"{A}/plans/{id}/active", new { isActive = active }, ct);
    public Task<ApiResult<bool>> DeletePlanAsync(int id, CancellationToken ct) => DeleteAsync($"{A}/plans/{id}", ct);
    public Task<ApiResult<PagedResult<SubscriptionInfo>>> SubscriptionsAsync(string? status, int page, int pageSize, CancellationToken ct) =>
        GetAsync<PagedResult<SubscriptionInfo>>(Url($"{A}/subscriptions", Query(("status", status), ("page", page), ("pageSize", pageSize))), ct);
    public Task<ApiResult<SubscriptionInfo>> AssignPlanAsync(Guid userId, string planCode, int months, CancellationToken ct) => PostAsync<SubscriptionInfo>($"{A}/subscriptions", new { userId, planCode, months }, ct);
    public Task<ApiResult<SubscriptionInfo>> ActivateSubscriptionAsync(Guid id, CancellationToken ct) => PatchAsync<SubscriptionInfo>($"{A}/subscriptions/{id}/activate", null, ct);
    public Task<ApiResult<SubscriptionInfo>> CancelSubscriptionAsync(Guid id, CancellationToken ct) => PatchAsync<SubscriptionInfo>($"{A}/subscriptions/{id}/cancel", null, ct);
    public Task<ApiResult<PagedResult<PaymentInfo>>> PaymentsAsync(bool? simulated, int page, int pageSize, CancellationToken ct) =>
        GetAsync<PagedResult<PaymentInfo>>(Url($"{A}/payments", Query(("simulated", simulated?.ToString().ToLowerInvariant()), ("page", page), ("pageSize", pageSize))), ct);
    public Task<ApiResult<PaymentInfo>> DemoPaymentAsync(Guid subscriptionId, CancellationToken ct) => PostAsync<PaymentInfo>($"{A}/payments/demo", new { subscriptionId }, ct);
    public Task<ApiResult<PagedResult<AuditLogInfo>>> AuditAsync(string? action, int page, int pageSize, CancellationToken ct) =>
        GetAsync<PagedResult<AuditLogInfo>>(Url($"{A}/audit-logs", Query(("action", action), ("page", page), ("pageSize", pageSize))), ct);
    public Task<ApiResult<PlanRevenueReport>> PlanRevenueAsync(CancellationToken ct) => GetAsync<PlanRevenueReport>($"{A}/reports/plan-revenue", ct);
    public Task<ApiResult<List<UsersByProfileTypeRow>>> UsersByProfileTypeAsync(CancellationToken ct) => GetAsync<List<UsersByProfileTypeRow>>($"{A}/reports/users-by-profile-type", ct);
    public Task<ApiResult<(byte[] Bytes, string ContentType)>> CsvAsync(string report, CancellationToken ct) => DownloadAsync($"{A}/reports/{report}", ct);
    public Task<ApiResult<List<T>>> CatalogListAsync<T>(string kind, string? query, CancellationToken ct) => GetAsync<List<T>>(Url($"{A}/catalogs/{kind}", query), ct);
    public Task<ApiResult<bool>> CatalogUpsertAsync(string kind, int? id, object body, CancellationToken ct) =>
        id is null ? PostAsync<bool>($"{A}/catalogs/{kind}", body, ct) : PutAsync<bool>($"{A}/catalogs/{kind}/{id}", body, ct);
    public Task<ApiResult<bool>> CatalogActiveAsync(string kind, int id, bool active, CancellationToken ct) => PatchAsync<bool>($"{A}/catalogs/{kind}/{id}/active", new { isActive = active }, ct);
    public Task<ApiResult<bool>> CatalogDeleteAsync(string kind, int id, CancellationToken ct) => DeleteAsync($"{A}/catalogs/{kind}/{id}", ct);
}
