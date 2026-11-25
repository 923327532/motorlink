using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MotorLink.API.Authorization;
using MotorLink.Application.Users;

namespace MotorLink.API.Controllers;

/// <summary>Perfil del usuario autenticado.</summary>
[ApiController]
[Route("api/v1/users")]
[Authorize(Policy = Policies.User)]
[Produces("application/json")]
public sealed class UsersController(IUserService users) : ControllerBase
{
    /// <summary>Obtiene el perfil propio. El usuario se toma del token, nunca de la URL (sin IDOR).</summary>
    [HttpGet("profile")]
    [ProducesResponseType<ProfileDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ProfileDto>> GetProfile(CancellationToken ct) =>
        await users.GetProfileAsync(User.GetUserId(), ct);

    /// <summary>Actualiza el perfil propio (PERSONAL o BUSINESS).</summary>
    [HttpPut("profile")]
    [ProducesResponseType<ProfileDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProfileDto>> UpdateProfile(UpdateProfileRequest request, CancellationToken ct) =>
        await users.UpdateProfileAsync(User.GetUserId(), request, ct);
}
