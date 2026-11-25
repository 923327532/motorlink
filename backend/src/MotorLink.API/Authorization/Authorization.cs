using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using MotorLink.Domain.Common;
using MotorLink.Domain.Entities;

namespace MotorLink.API.Authorization;

public static class Policies
{
    public const string AdminOnly = "AdminOnly";
    public const string User = "User";
    public const string AuthRateLimit = "auth";
    public const string PublicWriteRateLimit = "public-write";
    public const string ResourceOwner = "ResourceOwner";
}

/// <summary>Recurso que pertenece a un usuario (vehículo, publicación, negociación...).</summary>
public interface IOwnedResource
{
    Guid OwnerId { get; }
}

public sealed class ResourceOwnerRequirement : IAuthorizationRequirement
{
    public static readonly ResourceOwnerRequirement Instance = new();
}

/// <summary>
/// Autoriza si el usuario autenticado es el propietario del recurso o ADMIN.
/// Uso: await authorizationService.AuthorizeAsync(User, recurso, ResourceOwnerRequirement.Instance).
/// </summary>
public sealed class ResourceOwnerHandler : AuthorizationHandler<ResourceOwnerRequirement, IOwnedResource>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, ResourceOwnerRequirement requirement, IOwnedResource resource)
    {
        if (context.User.IsInRole(RoleNames.Admin) ||
            (context.User.GetUserIdOrNull() is { } id && id == resource.OwnerId))
            context.Succeed(requirement);
        return Task.CompletedTask;
    }
}

public static class ClaimsPrincipalExtensions
{
    public static Guid? GetUserIdOrNull(this ClaimsPrincipal user)
    {
        var sub = user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out var id) ? id : null;
    }

    public static Guid GetUserId(this ClaimsPrincipal user) =>
        user.GetUserIdOrNull() ?? throw new UnauthorizedAccessException("Token sin identificador de usuario.");
}
