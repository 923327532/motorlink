using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MotorLink.Web.Extensions;
using MotorLink.Web.Services;
using MotorLink.Web.Services.Api;
using MotorLink.Web.ViewModels;

namespace MotorLink.Web.Controllers;

/// <summary>Buscador y detalle público de publicaciones, más las acciones AJAX (vista, contacto y favoritos).</summary>
[Route("vehiculos")]
public sealed class MarketplaceController(IListingsApi listings, ICatalogApi catalogs, IFavoriteLookup favorites) : Controller
{
    [HttpGet(""), AllowAnonymous]
    public async Task<IActionResult> Index([FromQuery] MarketplaceQuery filters, CancellationToken ct)
    {
        var q = filters; // el parámetro no puede llamarse "q": colisionaría con el prefijo del modelo y se ignoraría ?q=
        q.Page = Math.Max(q.Page, 1);
        q.Operation = q.Operation is "SALE" or "RENT" or "WORK_RENT" ? q.Operation : null;

        var resultTask = listings.SearchAsync(q.ToApiQuery(), ct);
        var typesTask = catalogs.VehicleTypesAsync(ct);
        var brandsTask = catalogs.BrandsAsync(ct);
        var depsTask = catalogs.DepartmentsAsync(ct);
        var favTask = favorites.IdsAsync(ct);
        await Task.WhenAll(resultTask, typesTask, brandsTask, depsTask, favTask);

        var result = resultTask.Result;
        string? error = null;
        if (!result.Success)
        {
            if (result.Error!.Status != 400) throw new ApiPageException(result.Error);
            error = result.Error.FieldErrors.Values.SelectMany(v => v).FirstOrDefault() ?? result.Error.Message;
        }

        var vm = new MarketplaceViewModel
        {
            Query = q,
            Result = result.Success ? result.Value! : new PagedResult<ListingSummary>([], 1, q.PageSize, 0, 0),
            Types = typesTask.Result, Brands = brandsTask.Result, Departments = depsTask.Result, Favorites = favTask.Result,
            Chips = await BuildChipsAsync(q, typesTask.Result, brandsTask.Result, depsTask.Result, ct),
            Heading = q.Operation switch { "SALE" => "Vehículos en venta", "RENT" => "Vehículos en alquiler", "WORK_RENT" => "Vehículos para trabajar", _ => "Todos los vehículos" },
            Error = error
        };
        ViewData["Nav"] = q.Operation switch { "SALE" => "buy", "RENT" => "rent", "WORK_RENT" => "work", _ => null };
        return View(vm);
    }

    [HttpGet("{id:guid}"), AllowAnonymous]
    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        var detail = (await listings.DetailAsync(id, ct)).Data();
        var relatedTask = listings.SearchAsync($"q={Uri.EscapeDataString(detail.Vehicle.Brand)}&pageSize=5", ct);
        var favTask = favorites.IdsAsync(ct);
        await Task.WhenAll(relatedTask, favTask);

        var related = relatedTask.Result.Success ? relatedTask.Result.Value!.Items.Where(l => l.Id != id).Take(4).ToList() : [];
        var owner = User.UserId() is { } uid && uid == detail.Advertiser.UserId;
        ViewData["Title"] = $"{detail.Vehicle.Brand} {detail.Vehicle.Model} {detail.Vehicle.ModelYear}";
        ViewData["Description"] = detail.Title;
        return View(new DetailViewModel(detail, related, favTask.Result.Contains(id), owner));
    }

    // ---------------- acciones AJAX ----------------
    [HttpPost("{id:guid}/vista"), AllowAnonymous]
    public async Task<IActionResult> RegisterView(Guid id, CancellationToken ct)
    {
        await listings.RegisterViewAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/contacto"), AllowAnonymous]
    public async Task<IActionResult> Contact(Guid id, [FromBody] ContactBody body, CancellationToken ct)
    {
        var r = await listings.ContactAsync(id, body.Channel is "PHONE" ? "PHONE" : "WHATSAPP", ct);
        return r.Success ? Json(r.Value) : Json(new { ok = false, message = r.Error!.Message }, r.Error.Status);
    }

    [HttpPost("{id:guid}/favorito"), Authorize]
    public async Task<IActionResult> AddFavorite(Guid id, CancellationToken ct)
    {
        var r = await listings.AddFavoriteAsync(id, ct);
        return r.Success ? Json(new { ok = true }) : Json(new { ok = false, message = r.Error!.Message }, r.Error.Status);
    }

    [HttpDelete("{id:guid}/favorito"), Authorize]
    public async Task<IActionResult> RemoveFavorite(Guid id, CancellationToken ct)
    {
        var r = await listings.RemoveFavoriteAsync(id, ct);
        // Quitar un favorito inexistente no es un error para el usuario
        return r.Success || r.Error!.Status == 404 ? Json(new { ok = true }) : Json(new { ok = false, message = r.Error.Message }, r.Error.Status);
    }

    private JsonResult Json(object? value, int status) => new(value) { StatusCode = status };

    public sealed record ContactBody(string? Channel);

    // ---------------- filtros seleccionados (chips) ----------------
    private async Task<IReadOnlyList<FilterChip>> BuildChipsAsync(MarketplaceQuery q, IReadOnlyList<VehicleTypeDto> types, IReadOnlyList<BrandDto> brands,
        IReadOnlyList<DepartmentDto> deps, CancellationToken ct)
    {
        var chips = new List<FilterChip>();
        void Add(string label, params string[] removes) => chips.Add(new FilterChip(label, WithoutParams(removes)));

        if (q.Operation is not null) Add(Ui.Operation(q.Operation), "operation");
        if (!string.IsNullOrWhiteSpace(q.Q)) Add($"“{q.Q}”", "q");
        if (q.VehicleTypeId is { } t) Add(types.FirstOrDefault(x => x.Id == t)?.Name ?? "Tipo", "vehicleTypeId");
        if (q.BrandId is { } b) Add(brands.FirstOrDefault(x => x.Id == b)?.Name ?? "Marca", "brandId", "modelId");
        if (q.ModelId is { } m)
            Add((await catalogs.ModelsAsync(q.BrandId, q.VehicleTypeId, ct)).FirstOrDefault(x => x.Id == m)?.Name ?? "Modelo", "modelId");
        if (q.YearMin is not null || q.YearMax is not null) Add($"Año {q.YearMin?.ToString() ?? "…"}–{q.YearMax?.ToString() ?? "…"}", "yearMin", "yearMax");
        if (q.HasPriceFilter) Add($"{Ui.Symbol(q.Currency ?? "PEN")} {q.PriceMin?.ToString("0.##") ?? "…"} – {q.PriceMax?.ToString("0.##") ?? "…"}", "priceMin", "priceMax");
        if (!string.IsNullOrEmpty(q.Condition)) Add(Ui.Condition(q.Condition), "condition");
        if (q.MaxMileage is { } km) Add($"Hasta {Ui.Number(km)} km", "maxMileage");
        if (!string.IsNullOrEmpty(q.Fuel)) Add(Ui.Fuel(q.Fuel), "fuel");
        if (!string.IsNullOrEmpty(q.Transmission)) Add(Ui.Transmission(q.Transmission), "transmission");
        if (q.DepartmentId is { } d) Add(deps.FirstOrDefault(x => x.Id == d)?.Name ?? "Departamento", "departmentId", "provinceId", "districtId");
        if (q.ProvinceId is { } p) Add((await catalogs.ProvincesAsync(q.DepartmentId, ct)).FirstOrDefault(x => x.Id == p)?.Name ?? "Provincia", "provinceId", "districtId");
        if (q.DistrictId is { } di) Add((await catalogs.DistrictsAsync(q.ProvinceId, ct)).FirstOrDefault(x => x.Id == di)?.Name ?? "Distrito", "districtId");
        if (!string.IsNullOrEmpty(q.Availability) && q.Availability != "AVAILABLE")
            Add(q.Availability switch { "RESERVED" => "Reservados", "RENTED" => "Alquilados", _ => "Todas las disponibilidades" }, "availability");
        if (q.Negotiable == true) Add("Precio negociable", "negotiable");
        return chips;
    }

    /// <summary>URL actual sin los parámetros indicados (y sin la página).</summary>
    private string WithoutParams(string[] names)
    {
        var drop = new HashSet<string>(names.Append("page"), StringComparer.OrdinalIgnoreCase);
        var parts = Request.Query.Where(k => !drop.Contains(k.Key)).SelectMany(k => k.Value.Select(v => $"{Uri.EscapeDataString(k.Key)}={Uri.EscapeDataString(v ?? "")}")).ToList();
        return "/vehiculos" + (parts.Count > 0 ? "?" + string.Join('&', parts) : "");
    }
}

/// <summary>Catálogos en JSON para los selects en cascada (se leen de la API con caché breve).</summary>
[AllowAnonymous, Route("catalogos")]
public sealed class CatalogsController(ICatalogApi catalogs) : Controller
{
    [HttpGet("modelos")]
    public async Task<IActionResult> Models(int? brandId, int? vehicleTypeId, CancellationToken ct) =>
        Json((await catalogs.ModelsAsync(brandId, vehicleTypeId, ct)).Select(m => new { m.Id, m.Name, m.BrandId, m.VehicleTypeIds }));

    [HttpGet("provincias")]
    public async Task<IActionResult> Provinces(int? departmentId, CancellationToken ct) =>
        Json((await catalogs.ProvincesAsync(departmentId, ct)).Select(p => new { p.Id, p.Name }));

    [HttpGet("distritos")]
    public async Task<IActionResult> Districts(int? provinceId, CancellationToken ct) =>
        Json((await catalogs.DistrictsAsync(provinceId, ct)).Select(d => new { d.Id, d.Name }));
}

/// <summary>
/// Sirve las imágenes del respaldo local de la API (/media/...) para que el navegador solo hable con este sitio.
/// Con Cloudinary las URLs son absolutas y este controlador no interviene.
/// </summary>
[AllowAnonymous, Route("media")]
public sealed class MediaController(IHttpClientFactory http) : Controller
{
    [HttpGet("{**path}")]
    [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> Get(string path, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Contains("..") || !System.Text.RegularExpressions.Regex.IsMatch(path, @"^[A-Za-z0-9_\-./]+\.(png|jpe?g|webp)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            return NotFound();

        var client = http.CreateClient(Handlers.TokenProvider.AnonymousClientName);
        using var response = await client.GetAsync($"media/{path}", HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode) return NotFound();
        var bytes = await response.Content.ReadAsByteArrayAsync(ct);
        return File(bytes, response.Content.Headers.ContentType?.MediaType ?? "image/jpeg");
    }
}
