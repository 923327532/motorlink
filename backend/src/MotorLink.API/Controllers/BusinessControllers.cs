using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MotorLink.API.Authorization;
using MotorLink.Application.Common;
using MotorLink.Application.Dashboard;
using MotorLink.Application.Negotiations;
using MotorLink.Application.Notifications;
using MotorLink.Application.Subscriptions;
using MotorLink.Domain.Common;

namespace MotorLink.API.Controllers;

/// <summary>Propuestas y contrapropuestas de precio. Solo los participantes acceden; el resto recibe 404.</summary>
[ApiController]
[Route("api/v1/negotiations")]
[Authorize(Policy = Policies.User)]
[Produces("application/json")]
public sealed class NegotiationsController(INegotiationService negotiations) : ControllerBase
{
    /// <summary>Inicia una negociación con la primera oferta del interesado.</summary>
    [HttpPost]
    [ProducesResponseType<NegotiationDetailDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Start(StartNegotiationRequest request, CancellationToken ct)
    {
        var d = await negotiations.StartAsync(User.GetUserId(), request, ct);
        return Created($"/api/v1/negotiations/{d.Negotiation.Id}", d);
    }

    /// <summary>Negociaciones propias. role: SENT (como interesado) | RECEIVED (como propietario) | ALL.</summary>
    [HttpGet]
    public async Task<PagedResult<NegotiationDto>> List([FromQuery] NegotiationRole role = NegotiationRole.All, [FromQuery] NegotiationStatus? status = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        await negotiations.ListAsync(User.GetUserId(), role, status, page, pageSize, ct);

    /// <summary>Detalle con el historial completo de ofertas.</summary>
    [HttpGet("{id:guid}")]
    public async Task<NegotiationDetailDto> Get(Guid id, CancellationToken ct) => await negotiations.GetAsync(User.GetUserId(), id, ct);

    /// <summary>Envía una contrapropuesta (solo la contraparte de la última oferta).</summary>
    [HttpPost("{id:guid}/offers")]
    public async Task<IActionResult> Counter(Guid id, CounterOfferRequest request, CancellationToken ct)
    {
        var d = await negotiations.CounterAsync(User.GetUserId(), id, request, ct);
        return Created($"/api/v1/negotiations/{id}", d);
    }

    /// <summary>Acepta la ÚLTIMA oferta (offerId debe coincidir). Es un acuerdo de precio, no de pago o entrega.</summary>
    [HttpPatch("{id:guid}/accept")]
    public async Task<NegotiationDetailDto> Accept(Guid id, AcceptOfferRequest request, CancellationToken ct) =>
        await negotiations.AcceptAsync(User.GetUserId(), id, request.OfferId, ct);

    [HttpPatch("{id:guid}/reject")]
    public async Task<NegotiationDetailDto> Reject(Guid id, CancellationToken ct) => await negotiations.RejectAsync(User.GetUserId(), id, ct);

    [HttpPatch("{id:guid}/cancel")]
    public async Task<NegotiationDetailDto> Cancel(Guid id, CancellationToken ct) => await negotiations.CancelAsync(User.GetUserId(), id, ct);
}

[ApiController]
[Route("api/v1/plans")]
[AllowAnonymous]
[Produces("application/json")]
public sealed class PlansController(IPlanService plans) : ControllerBase
{
    /// <summary>Planes mensuales activos con sus precios, límites y beneficios (datos de la base de datos).</summary>
    [HttpGet]
    public async Task<IReadOnlyList<PlanDto>> List(CancellationToken ct) => await plans.ListActiveAsync(ct);
}

[ApiController]
[Route("api/v1/subscriptions")]
[Authorize(Policy = Policies.User)]
[Produces("application/json")]
public sealed class SubscriptionsController(ISubscriptionService subscriptions) : ControllerBase
{
    /// <summary>Plan vigente, su suscripción y el consumo de límites.</summary>
    [HttpGet("current")]
    public async Task<CurrentPlanDto> Current(CancellationToken ct) => await subscriptions.GetCurrentAsync(User.GetUserId(), ct);

    [HttpGet]
    public async Task<PagedResult<SubscriptionDto>> History([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        await subscriptions.HistoryAsync(User.GetUserId(), page, pageSize, ct);

    [HttpGet("payments")]
    public async Task<PagedResult<PaymentDto>> Payments([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        await subscriptions.PaymentsAsync(User.GetUserId(), page, pageSize, ct);

    /// <summary>Solicita un cambio de plan (crea una suscripción PENDING). Elegir FREE cancela la renovación del plan de pago.</summary>
    [HttpPost("change-plan")]
    [ProducesResponseType<SubscriptionDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> ChangePlan(CheckoutRequest request, CancellationToken ct) =>
        Created("/api/v1/subscriptions", await subscriptions.ChangePlanAsync(User.GetUserId(), request.PlanCode, ct));

    /// <summary>Solicita la renovación del plan actual (suscripción PENDING).</summary>
    [HttpPost("renew")]
    public async Task<IActionResult> Renew(CancellationToken ct) =>
        Created("/api/v1/subscriptions", await subscriptions.RenewAsync(User.GetUserId(), ct));

    /// <summary>Completa el pago DE DEMOSTRACIÓN (si está habilitado) y activa la suscripción. No se cobra dinero real.</summary>
    [HttpPost("{id:guid}/pay")]
    public async Task<SubscriptionDto> Pay(Guid id, CancellationToken ct) => await subscriptions.PayAsync(User.GetUserId(), id, ct);

    /// <summary>Cancela una suscripción pendiente, o detiene la renovación de la activa (conserva el plan hasta su vencimiento).</summary>
    [HttpPatch("{id:guid}/cancel")]
    public async Task<SubscriptionDto> Cancel(Guid id, CancellationToken ct) => await subscriptions.CancelAsync(User.GetUserId(), id, ct);
}

[ApiController]
[Route("api/v1/dashboard")]
[Authorize(Policy = Policies.User)]
[Produces("application/json")]
public sealed class DashboardController(IDashboardService dashboard) : ControllerBase
{
    /// <summary>Indicadores reales del usuario. Rango por defecto: últimos 30 días. La evolución mensual requiere estadísticas avanzadas.</summary>
    [HttpGet]
    public async Task<DashboardDto> Get([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct) =>
        await dashboard.GetAsync(User.GetUserId(), from, to, ct);

    /// <summary>Exportación CSV (requiere un plan con exportación de reportes).</summary>
    [HttpGet("export")]
    [Produces("text/csv")]
    public async Task<IActionResult> Export([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct) =>
        File(System.Text.Encoding.UTF8.GetBytes(await dashboard.ExportCsvAsync(User.GetUserId(), from, to, ct)), "text/csv", "motorlink-dashboard.csv");
}

[ApiController]
[Route("api/v1/notifications")]
[Authorize(Policy = Policies.User)]
[Produces("application/json")]
public sealed class NotificationsController(INotificationService notifications) : ControllerBase
{
    [HttpGet]
    public async Task<PagedResult<NotificationDto>> List([FromQuery] bool unreadOnly = false, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        await notifications.ListAsync(User.GetUserId(), unreadOnly, page, pageSize, ct);

    [HttpGet("unread-count")]
    public async Task<object> UnreadCount(CancellationToken ct) => new { count = await notifications.UnreadCountAsync(User.GetUserId(), ct) };

    [HttpPatch("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        await notifications.MarkReadAsync(User.GetUserId(), id, ct);
        return NoContent();
    }

    [HttpPatch("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        await notifications.MarkAllReadAsync(User.GetUserId(), ct);
        return NoContent();
    }
}
