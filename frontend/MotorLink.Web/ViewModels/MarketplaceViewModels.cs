using MotorLink.Web.Services.Api;

namespace MotorLink.Web.ViewModels;

public sealed record CategoryTile(string Label, string Icon, int? TypeId, int Count);

public sealed record HomeViewModel(
    IReadOnlyList<ListingSummary> Featured, IReadOnlyList<ListingSummary> Work, IReadOnlyList<ListingSummary> Latest, IReadOnlyList<PlanInfo> Plans,
    IReadOnlyList<VehicleTypeDto> Types, IReadOnlyList<DepartmentDto> Departments, IReadOnlyList<CategoryTile> Categories, IReadOnlySet<Guid> Favorites);

/// <summary>Parámetros del buscador. Los nombres coinciden con los de la API para poder reenviarlos tal cual.</summary>
public sealed class MarketplaceQuery
{
    public string? Operation { get; set; }
    public string? Q { get; set; }
    public int? VehicleTypeId { get; set; }
    public int? BrandId { get; set; }
    public int? ModelId { get; set; }
    public int? YearMin { get; set; }
    public int? YearMax { get; set; }
    public decimal? PriceMin { get; set; }
    public decimal? PriceMax { get; set; }
    public string? Currency { get; set; }
    public string? Condition { get; set; }
    public int? MaxMileage { get; set; }
    public string? Fuel { get; set; }
    public string? Transmission { get; set; }
    public int? DepartmentId { get; set; }
    public int? ProvinceId { get; set; }
    public int? DistrictId { get; set; }
    public string? Availability { get; set; }
    public bool? Negotiable { get; set; }
    public string? Sort { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 12;

    public bool HasPriceFilter => PriceMin is not null || PriceMax is not null;

    /// <summary>Cadena de consulta para la API. La moneda es obligatoria al filtrar u ordenar por precio (por defecto PEN).</summary>
    public string ToApiQuery()
    {
        var needsCurrency = HasPriceFilter || Sort is "PRICE_ASC" or "PRICE_DESC";
        return ApiQuery.Build(("operation", Operation), ("q", Q), ("vehicleTypeId", VehicleTypeId), ("brandId", BrandId), ("modelId", ModelId),
            ("yearMin", YearMin), ("yearMax", YearMax), ("priceMin", PriceMin), ("priceMax", PriceMax),
            ("currency", needsCurrency ? (string.IsNullOrEmpty(Currency) ? "PEN" : Currency) : Currency),
            ("condition", Condition), ("maxMileage", MaxMileage), ("fuel", Fuel), ("transmission", Transmission),
            ("departmentId", DepartmentId), ("provinceId", ProvinceId), ("districtId", DistrictId),
            ("availability", Availability), ("negotiable", Negotiable == true ? "true" : null), ("sort", Sort), ("page", Page), ("pageSize", PageSize));
    }
}

public static class ApiQuery
{
    public static string Build(params (string Key, object? Value)[] pairs) =>
        string.Join('&', pairs.Where(p => p.Value is not null && Convert.ToString(p.Value, System.Globalization.CultureInfo.InvariantCulture) != "")
            .Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(Convert.ToString(p.Value, System.Globalization.CultureInfo.InvariantCulture)!)}"));
}

public sealed record FilterChip(string Label, string RemoveUrl);

public sealed class MarketplaceViewModel
{
    public required MarketplaceQuery Query { get; init; }
    public required PagedResult<ListingSummary> Result { get; init; }
    public required IReadOnlyList<VehicleTypeDto> Types { get; init; }
    public required IReadOnlyList<BrandDto> Brands { get; init; }
    public required IReadOnlyList<DepartmentDto> Departments { get; init; }
    public required IReadOnlySet<Guid> Favorites { get; init; }
    public required IReadOnlyList<FilterChip> Chips { get; init; }
    public string Heading { get; init; } = "Vehículos";
    public string? Error { get; init; }
}

public sealed record DetailViewModel(ListingDetail Detail, IReadOnlyList<ListingSummary> Related, bool IsFavorite, bool IsOwner);
