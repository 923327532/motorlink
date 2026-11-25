using MotorLink.Web.Services.Api;

namespace MotorLink.Web.Services;

/// <summary>Conjunto de publicaciones favoritas del usuario actual (una consulta por solicitud, hasta 200 elementos).</summary>
public interface IFavoriteLookup
{
    Task<IReadOnlySet<Guid>> IdsAsync(CancellationToken ct);
}

public sealed class FavoriteLookup(IListingsApi listings, IHttpContextAccessor accessor) : IFavoriteLookup
{
    private IReadOnlySet<Guid>? _cache;

    public async Task<IReadOnlySet<Guid>> IdsAsync(CancellationToken ct)
    {
        if (_cache is not null) return _cache;
        var ids = new HashSet<Guid>();
        if (accessor.HttpContext?.User.Identity?.IsAuthenticated == true)
        {
            try
            {
                for (var page = 1; page <= 4; page++)
                {
                    var r = await listings.FavoritesAsync(page, 50, ct);
                    if (!r.Success) break;
                    foreach (var i in r.Value!.Items) ids.Add(i.Id);
                    if (page >= r.Value.TotalPages) break;
                }
            }
            catch (Exception ex) when (ex is ApiUnauthorizedException or ApiForbiddenException) { /* la sesión se gestiona en la acción */ }
        }
        return _cache = ids;
    }
}
