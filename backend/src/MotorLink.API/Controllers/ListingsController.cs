using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MotorLink.API.Authorization;
using MotorLink.Application.Common;
using MotorLink.Application.Listings;
using MotorLink.Domain.Common;

namespace MotorLink.API.Controllers;

/// <summary>Búsqueda pública de publicaciones y gestión de las publicaciones propias.</summary>
[ApiController]
[Route("api/v1/listings")]
[Produces("application/json")]
public sealed class ListingsController(IPublicListingService publicListings, IListingService listings) : ControllerBase
{
    private Guid? CurrentUserId => User.Identity?.IsAuthenticated == true ? User.GetUserIdOrNull() : null;

    // ======================= PÚBLICO =======================

    /// <summary>
    /// Búsqueda con filtros, ordenamiento y paginación ejecutados en SQL Server. Los filtros/orden por precio exigen
    /// <c>currency</c>: nunca se comparan monedas distintas.
    /// </summary>
    [HttpGet, AllowAnonymous]
    [ProducesResponseType<PagedResult<ListingSummaryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<PagedResult<ListingSummaryDto>> Search([FromQuery] ListingSearchQuery query, CancellationToken ct) =>
        await publicListings.SearchAsync(query, ct);

    /// <summary>Detalle público: sin placa completa, sin teléfonos y sin datos de titularidad.</summary>
    [HttpGet("{id:guid}"), AllowAnonymous]
    [ProducesResponseType<ListingDetailDto>(StatusCodes.Status200OK)]
    public async Task<ListingDetailDto> GetDetail(Guid id, CancellationToken ct) => await publicListings.GetDetailAsync(id, ct);

    /// <summary>Información pública del anunciante.</summary>
    [HttpGet("{id:guid}/advertiser"), AllowAnonymous]
    public async Task<PublicAdvertiserDto> GetAdvertiser(Guid id, CancellationToken ct) => await publicListings.GetAdvertiserAsync(id, ct);

    /// <summary>Registra una visualización (las del propio anunciante y repetidas en 30 min no cuentan).</summary>
    [HttpPost("{id:guid}/views"), AllowAnonymous, EnableRateLimiting(Policies.PublicWriteRateLimit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RegisterView(Guid id, CancellationToken ct)
    {
        await publicListings.RegisterViewAsync(id, CurrentUserId, ct);
        return NoContent();
    }

    /// <summary>
    /// Registra el clic de contacto y devuelve el teléfono o el enlace directo de WhatsApp. Es un evento de intención:
    /// no confirma que el contacto comercial se haya concretado.
    /// </summary>
    [HttpPost("{id:guid}/contact"), AllowAnonymous, EnableRateLimiting(Policies.PublicWriteRateLimit)]
    [ProducesResponseType<ContactInfoDto>(StatusCodes.Status200OK)]
    public async Task<ContactInfoDto> Contact(Guid id, ContactRequest request, CancellationToken ct) =>
        await publicListings.RegisterContactAsync(id, CurrentUserId, request.Channel, ct);

    // ======================= PRIVADO =======================

    [HttpGet("mine"), Authorize(Policy = Policies.User)]
    public async Task<PagedResult<ListingDto>> ListMine([FromQuery] ListingStatus? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        await listings.ListMineAsync(User.GetUserId(), status, page, pageSize, ct);

    [HttpGet("mine/{id:guid}"), Authorize(Policy = Policies.User)]
    public async Task<ListingDto> GetMine(Guid id, CancellationToken ct) => await listings.GetMineAsync(User.GetUserId(), id, ct);

    /// <summary>Crea una publicación en borrador con una o más modalidades (SALE, RENT, WORK_RENT).</summary>
    [HttpPost, Authorize(Policy = Policies.User)]
    [ProducesResponseType<ListingDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(ListingRequest request, CancellationToken ct)
    {
        var dto = await listings.CreateAsync(User.GetUserId(), request, ct);
        return Created($"/api/v1/listings/mine/{dto.Id}", dto);
    }

    /// <summary>Edita una publicación en DRAFT, REJECTED o PAUSED.</summary>
    [HttpPut("{id:guid}"), Authorize(Policy = Policies.User)]
    public async Task<ListingDto> Update(Guid id, ListingRequest request, CancellationToken ct) =>
        await listings.UpdateAsync(User.GetUserId(), id, request, ct);

    /// <summary>
    /// Cambia el estado respetando la máquina de estados. PENDING_REVIEW y reactivaciones validan los límites del plan.
    /// RESERVED/RENTED requieren <c>operationType</c>.
    /// </summary>
    [HttpPatch("{id:guid}/status"), Authorize(Policy = Policies.User)]
    [ProducesResponseType<ListingDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ListingDto> ChangeStatus(Guid id, ChangeListingStatusRequest request, CancellationToken ct) =>
        await listings.ChangeStatusAsync(User.GetUserId(), id, request, ct);

    /// <summary>Destaca o quita el destacado (sujeto al límite de destacados del plan).</summary>
    [HttpPatch("{id:guid}/featured"), Authorize(Policy = Policies.User)]
    public async Task<ListingDto> SetFeatured(Guid id, SetFeaturedRequest request, CancellationToken ct) =>
        await listings.SetFeaturedAsync(User.GetUserId(), id, request.IsFeatured, ct);

    [HttpDelete("{id:guid}"), Authorize(Policy = Policies.User)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await listings.DeleteAsync(User.GetUserId(), id, ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/v1/favorites")]
[Authorize(Policy = Policies.User)]
[Produces("application/json")]
public sealed class FavoritesController(IFavoriteService favorites) : ControllerBase
{
    [HttpGet]
    public async Task<PagedResult<ListingSummaryDto>> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        await favorites.ListAsync(User.GetUserId(), page, pageSize, ct);

    /// <summary>Agrega a favoritos de forma idempotente: 201 si se creó, 204 si ya existía (sin duplicados).</summary>
    [HttpPut("{listingId:guid}")]
    public async Task<IActionResult> Add(Guid listingId, CancellationToken ct) =>
        await favorites.AddAsync(User.GetUserId(), listingId, ct) ? Created($"/api/v1/favorites", new { listingId }) : NoContent();

    [HttpDelete("{listingId:guid}")]
    public async Task<IActionResult> Remove(Guid listingId, CancellationToken ct)
    {
        await favorites.RemoveAsync(User.GetUserId(), listingId, ct);
        return NoContent();
    }
}
