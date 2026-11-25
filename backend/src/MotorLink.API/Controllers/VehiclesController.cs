using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MotorLink.API.Authorization;
using MotorLink.Application.Common;
using MotorLink.Application.Vehicles;

namespace MotorLink.API.Controllers;

public sealed record ReorderImagesRequest([Required, MinLength(1)] List<Guid> ImageIds);

/// <summary>Vehículos del usuario autenticado y sus fotografías. Los recursos ajenos responden 404.</summary>
[ApiController]
[Route("api/v1/vehicles")]
[Authorize(Policy = Policies.User)]
[Produces("application/json")]
public sealed class VehiclesController(IVehicleService vehicles, IVehicleImageService images) : ControllerBase
{
    /// <summary>Registra un vehículo. Valida los campos técnicos según el tipo y la compatibilidad marca/modelo/tipo.</summary>
    [HttpPost]
    [ProducesResponseType<VehicleDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(VehicleRequest request, CancellationToken ct)
    {
        var v = await vehicles.CreateAsync(User.GetUserId(), request, ct);
        return Created($"/api/v1/vehicles/{v.Id}", v);
    }

    [HttpGet]
    public async Task<PagedResult<VehicleDto>> ListMine([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        await vehicles.ListMineAsync(User.GetUserId(), page, pageSize, ct);

    [HttpGet("{id:guid}")]
    [ProducesResponseType<VehicleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<VehicleDto> Get(Guid id, CancellationToken ct) => await vehicles.GetMineAsync(User.GetUserId(), id, ct);

    [HttpPut("{id:guid}")]
    public async Task<VehicleDto> Update(Guid id, VehicleRequest request, CancellationToken ct) =>
        await vehicles.UpdateAsync(User.GetUserId(), id, request, ct);

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await vehicles.DeleteAsync(User.GetUserId(), id, ct);
        return NoContent();
    }

    // ---------------- fotografías ----------------
    [HttpGet("{id:guid}/images")]
    public async Task<IReadOnlyList<VehicleImageDto>> ListImages(Guid id, CancellationToken ct) =>
        await images.ListAsync(User.GetUserId(), id, ct);

    /// <summary>Carga una fotografía (multipart, campo "file"). Máx. 5 MB; JPEG, PNG o WebP verificados por su contenido.</summary>
    [HttpPost("{id:guid}/images")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    [ProducesResponseType<VehicleImageDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UploadImage(Guid id, IFormFile file, CancellationToken ct)
    {
        if (file.Length > ImageSignature.MaxBytes)
            throw Domain.Exceptions.BusinessValidationException.For("file", "La imagen supera el máximo de 5 MB.");
        await using var stream = file.OpenReadStream();
        var dto = await images.UploadAsync(User.GetUserId(), id, stream, file.ContentType, ct);
        return Created($"/api/v1/vehicles/{id}/images", dto);
    }

    [HttpDelete("{id:guid}/images/{imageId:guid}")]
    public async Task<IActionResult> DeleteImage(Guid id, Guid imageId, CancellationToken ct)
    {
        await images.DeleteAsync(User.GetUserId(), id, imageId, ct);
        return NoContent();
    }

    /// <summary>Define el orden completo de las fotografías.</summary>
    [HttpPut("{id:guid}/images/order")]
    public async Task<IReadOnlyList<VehicleImageDto>> Reorder(Guid id, ReorderImagesRequest request, CancellationToken ct) =>
        await images.ReorderAsync(User.GetUserId(), id, request.ImageIds, ct);

    /// <summary>Selecciona la fotografía de portada.</summary>
    [HttpPatch("{id:guid}/images/{imageId:guid}/cover")]
    public async Task<IReadOnlyList<VehicleImageDto>> SetCover(Guid id, Guid imageId, CancellationToken ct) =>
        await images.SetCoverAsync(User.GetUserId(), id, imageId, ct);
}
