using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MotorLink.API.Authorization;
using MotorLink.Application.Abstractions;
using MotorLink.Application.Admin;
using MotorLink.Application.Common;
using MotorLink.Application.Listings;
using MotorLink.Application.Subscriptions;
using MotorLink.Domain.Common;

namespace MotorLink.API.Controllers;

[ApiController]
[Route("api/v1/admin/users")]
[Authorize(Policy = Policies.AdminOnly)]
[Produces("application/json")]
public sealed class AdminUsersController(IAdminService admin) : ControllerBase
{
    [HttpGet]
    public async Task<PagedResult<AdminUserDto>> List([FromQuery] string? search, [FromQuery] bool? active, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        await admin.ListUsersAsync(search, active, page, pageSize, ct);

    [HttpGet("{id:guid}")]
    public async Task<AdminUserDto> Get(Guid id, CancellationToken ct) => await admin.GetUserAsync(id, ct);

    /// <summary>Bloquea (isActive=false, revoca sesiones) o reactiva una cuenta.</summary>
    [HttpPatch("{id:guid}/status")]
    public async Task<AdminUserDto> SetStatus(Guid id, SetUserActiveRequest request, CancellationToken ct) =>
        await admin.SetUserActiveAsync(User.GetUserId(), id, request.IsActive, ct);
}

[ApiController]
[Route("api/v1/admin/listings")]
[Authorize(Policy = Policies.AdminOnly)]
[Produces("application/json")]
public sealed class AdminListingsController(IListingModerationService moderation) : ControllerBase
{
    /// <summary>Cola de moderación (por defecto PENDING_REVIEW).</summary>
    [HttpGet]
    public async Task<PagedResult<ModerationItemDto>> List([FromQuery] ListingStatus? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        await moderation.ListAsync(status, page, pageSize, ct);

    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct)
    {
        await moderation.ApproveAsync(User.GetUserId(), id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, RejectListingRequest request, CancellationToken ct)
    {
        await moderation.RejectAsync(User.GetUserId(), id, request.Reason, ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/v1/admin/plans")]
[Authorize(Policy = Policies.AdminOnly)]
[Produces("application/json")]
public sealed class AdminPlansController(IPlanService plans) : ControllerBase
{
    [HttpGet] public async Task<IReadOnlyList<PlanDto>> List(CancellationToken ct) => await plans.ListAllAsync(ct);

    [HttpPost]
    public async Task<IActionResult> Create(PlanRequest r, CancellationToken ct) { var p = await plans.UpsertAsync(null, r, ct); return Created($"/api/v1/admin/plans/{p.Id}", p); }

    [HttpPut("{id:int}")] public async Task<PlanDto> Update(int id, PlanRequest r, CancellationToken ct) => await plans.UpsertAsync(id, r, ct);

    [HttpPatch("{id:int}/active")]
    public async Task<IActionResult> SetActive(int id, ActiveRequest r, CancellationToken ct) { await plans.SetActiveAsync(id, r.IsActive, ct); return NoContent(); }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct) { await plans.DeleteAsync(id, ct); return NoContent(); }
}

[ApiController]
[Route("api/v1/admin")]
[Authorize(Policy = Policies.AdminOnly)]
[Produces("application/json")]
public sealed class AdminSubscriptionsController(ISubscriptionService subscriptions) : ControllerBase
{
    [HttpGet("subscriptions")]
    public async Task<PagedResult<SubscriptionDto>> List([FromQuery] SubscriptionStatus? status, [FromQuery] Guid? userId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        await subscriptions.AdminListAsync(status, userId, page, pageSize, ct);

    /// <summary>Asigna y activa un plan a un usuario sin pago (queda como COMPLIMENTARY).</summary>
    [HttpPost("subscriptions")]
    public async Task<IActionResult> Assign(AdminAssignRequest request, CancellationToken ct) =>
        Created("/api/v1/admin/subscriptions", await subscriptions.AdminAssignAsync(request, ct));

    /// <summary>Activación administrativa de una suscripción PENDING.</summary>
    [HttpPatch("subscriptions/{id:guid}/activate")]
    public async Task<SubscriptionDto> Activate(Guid id, CancellationToken ct) => await subscriptions.AdminActivateAsync(id, ct);

    [HttpPatch("subscriptions/{id:guid}/cancel")]
    public async Task<SubscriptionDto> Cancel(Guid id, CancellationToken ct) => await subscriptions.AdminCancelAsync(id, ct);

    [HttpGet("payments")]
    public async Task<PagedResult<PaymentDto>> Payments([FromQuery] bool? simulated, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        await subscriptions.AdminPaymentsAsync(simulated, page, pageSize, ct);

    /// <summary>Registra un pago de DEMOSTRACIÓN (IsSimulated=true) y activa la suscripción. No es dinero real.</summary>
    [HttpPost("payments/demo")]
    public async Task<IActionResult> DemoPayment(DemoPaymentRequest request, CancellationToken ct) =>
        Created("/api/v1/admin/payments", await subscriptions.AdminRegisterDemoPaymentAsync(request.SubscriptionId, ct));
}

[ApiController]
[Route("api/v1/admin")]
[Authorize(Policy = Policies.AdminOnly)]
[Produces("application/json")]
public sealed class AdminReportsController(
    IAdminService admin, IReportQueries reports, IPlanRevenueReport planRevenue) : ControllerBase
{
    [HttpGet("audit-logs")]
    public async Task<PagedResult<AuditLogDto>> Audit([FromQuery] string? action, [FromQuery] Guid? userId, [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        await admin.ListAuditAsync(action, userId, from, to, page, pageSize, ct);

    /// <summary>Estadísticas generales, con MRR real y simulado por separado.</summary>
    [HttpGet("stats")]
    public async Task<AdminStatsDto> Stats(CancellationToken ct) => await admin.GetStatsAsync(ct);

    /// <summary>Reporte ADO.NET (SqlDataReader): usuarios por tipo de perfil.</summary>
    [HttpGet("reports/users-by-profile-type")]
    public async Task<IReadOnlyList<UsersByProfileTypeRow>> UsersByProfileType(CancellationToken ct) =>
        await reports.GetUsersByProfileTypeAsync(ct);

    /// <summary>Reporte ADO.NET desconectado (SqlDataAdapter + DataSet + DataRelation): ingresos mensuales por plan y origen.</summary>
    [HttpGet("reports/plan-revenue")]
    public async Task<PlanRevenueReport> PlanRevenue(CancellationToken ct) => await planRevenue.GenerateAsync(ct);

    [HttpGet("reports/users.csv")]
    [Produces("text/csv")]
    public async Task<IActionResult> UsersCsv(CancellationToken ct) =>
        File(Encoding.UTF8.GetBytes(await admin.ExportUsersCsvAsync(ct)), "text/csv", "users.csv");

    [HttpGet("reports/subscriptions.csv")]
    [Produces("text/csv")]
    public async Task<IActionResult> SubscriptionsCsv(CancellationToken ct) =>
        File(Encoding.UTF8.GetBytes(await admin.ExportSubscriptionsCsvAsync(ct)), "text/csv", "subscriptions.csv");
}

/// <summary>Listados administrativos de solo lectura (sin placas completas ni documentos).</summary>
[ApiController]
[Route("api/v1/admin")]
[Authorize(Policy = Policies.AdminOnly)]
[Produces("application/json")]
public sealed class AdminDirectoryController(IAdminDirectoryService directory) : ControllerBase
{
    [HttpGet("businesses")]
    public async Task<PagedResult<AdminBusinessDto>> Businesses([FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        await directory.BusinessesAsync(search, page, pageSize, ct);

    [HttpGet("vehicles")]
    public async Task<PagedResult<AdminVehicleDto>> Vehicles([FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        await directory.VehiclesAsync(search, page, pageSize, ct);

    [HttpGet("negotiations")]
    public async Task<PagedResult<AdminNegotiationDto>> Negotiations([FromQuery] NegotiationStatus? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        await directory.NegotiationsAsync(status, page, pageSize, ct);
}
