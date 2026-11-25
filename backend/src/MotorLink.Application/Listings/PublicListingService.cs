using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using MotorLink.Application.Common;
using MotorLink.Application.Vehicles;
using MotorLink.Domain.Common;
using MotorLink.Domain.Entities;
using MotorLink.Domain.Exceptions;
using MotorLink.Domain.Rules;

namespace MotorLink.Application.Listings;

public interface IPublicListingService
{
    Task<PagedResult<ListingSummaryDto>> SearchAsync(ListingSearchQuery q, CancellationToken ct);
    Task<ListingDetailDto> GetDetailAsync(Guid id, CancellationToken ct);
    Task<PublicAdvertiserDto> GetAdvertiserAsync(Guid listingId, CancellationToken ct);
    Task RegisterViewAsync(Guid listingId, Guid? viewerId, CancellationToken ct);
    Task<ContactInfoDto> RegisterContactAsync(Guid listingId, Guid? userId, ContactChannel channel, CancellationToken ct);
}

public sealed class PublicListingService(IApplicationDbContext db, TimeProvider clock) : IPublicListingService
{
    private static readonly ListingStatus[] Visible = ListingStateMachine.PublicVisible;

    public async Task<PagedResult<ListingSummaryDto>> SearchAsync(ListingSearchQuery s, CancellationToken ct)
    {
        Validate(s);
        var (page, pageSize) = Paging.Normalize(s.Page, s.PageSize);

        IQueryable<Listing> q = db.Listings.AsNoTracking().Where(l => Visible.Contains(l.Status) && !l.Vehicle.IsDeleted);

        q = s.Availability switch
        {
            ListingAvailability.Available => q.Where(l => l.Status == ListingStatus.Published),
            ListingAvailability.Reserved => q.Where(l => l.Status == ListingStatus.Reserved),
            ListingAvailability.Rented => q.Where(l => l.Status == ListingStatus.Rented),
            _ => q
        };

        // Modalidad: precio, moneda, negociable, disponibilidad y tipo se evalúan sobre LA MISMA modalidad.
        var onlyAvailableOps = s.Availability == ListingAvailability.Available;
        Expression<Func<ListingOperation, bool>> op = o =>
            o.IsActive
            && (!onlyAvailableOps || o.Status == OperationStatus.Available)
            && (s.Operation == null || o.OperationType == s.Operation)
            && (s.Currency == null || o.Currency == s.Currency)
            && (s.PriceMin == null || o.ReferencePrice >= s.PriceMin)
            && (s.PriceMax == null || o.ReferencePrice <= s.PriceMax)
            && (s.Negotiable == null || o.IsNegotiable == s.Negotiable)
            && (s.AvailableBy == null || o.AvailableFrom == null || o.AvailableFrom <= s.AvailableBy)
            && (s.WorkUse == null || (o.WorkRent != null && o.WorkRent.AllowedUse == s.WorkUse));
        q = q.Where(l => l.Operations.AsQueryable().Any(op));

        if (s.VehicleTypeId is { } vt) q = q.Where(l => l.Vehicle.VehicleTypeId == vt);
        if (s.BrandId is { } br) q = q.Where(l => l.Vehicle.Model.BrandId == br);
        if (s.ModelId is { } md) q = q.Where(l => l.Vehicle.ModelId == md);
        if (s.YearMin is { } y1) q = q.Where(l => l.Vehicle.ModelYear >= y1);
        if (s.YearMax is { } y2) q = q.Where(l => l.Vehicle.ModelYear <= y2);
        if (s.Condition is { } cd) q = q.Where(l => l.Vehicle.Condition == cd);
        if (s.MaxMileage is { } km) q = q.Where(l => l.Vehicle.Mileage <= km);
        if (s.Fuel is { } fu) q = q.Where(l => l.Vehicle.FuelType == fu);
        if (s.Transmission is { } tr) q = q.Where(l => l.Vehicle.Transmission == tr);
        if (s.DepartmentId is { } dp) q = q.Where(l => l.DepartmentId == dp);
        if (s.ProvinceId is { } pv) q = q.Where(l => l.ProvinceId == pv);
        if (s.DistrictId is { } ds) q = q.Where(l => l.DistrictId == ds);

        if (!string.IsNullOrWhiteSpace(s.Q))
            foreach (var term in s.Q.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(5))
            {
                var t = term;
                q = q.Where(l => l.Title.Contains(t) || l.Vehicle.Model.Name.Contains(t) || l.Vehicle.Model.Brand.Name.Contains(t)
                                 || (l.Vehicle.Version != null && l.Vehicle.Version.Contains(t)) || (l.Vehicle.Color != null && l.Vehicle.Color.Contains(t))
                                 || l.Vehicle.VehicleType.Name.Contains(t));
            }

        q = s.Sort switch
        {
            ListingSort.PriceAsc => q.OrderBy(l => l.Operations.AsQueryable().Where(op).Min(o => o.ReferencePrice)).ThenByDescending(l => l.PublishedAt),
            ListingSort.PriceDesc => q.OrderByDescending(l => l.Operations.AsQueryable().Where(op).Min(o => o.ReferencePrice)).ThenByDescending(l => l.PublishedAt),
            ListingSort.YearDesc => q.OrderByDescending(l => l.Vehicle.ModelYear).ThenByDescending(l => l.PublishedAt),
            ListingSort.YearAsc => q.OrderBy(l => l.Vehicle.ModelYear).ThenByDescending(l => l.PublishedAt),
            _ => q.OrderByDescending(l => l.IsFeatured).ThenByDescending(l => l.PublishedAt)
        };

        // Paginación y proyección en SQL Server: solo se leen las columnas del DTO (sin N+1).
        var total = await q.CountAsync(ct);
        var items = total == 0 ? [] : await q.Skip((page - 1) * pageSize).Take(pageSize).Select(l => new ListingSummaryDto(
            l.Id, l.Title, l.Status == ListingStatus.Published, l.Status, l.IsFeatured,
            l.Vehicle.Images.OrderByDescending(i => i.IsCover).ThenBy(i => i.SortOrder).Select(i => i.Url).FirstOrDefault(),
            l.Vehicle.VehicleType.Name, l.Vehicle.Model.Brand.Name, l.Vehicle.Model.Name, l.Vehicle.Version, l.Vehicle.ModelYear,
            l.Vehicle.Mileage, l.Vehicle.Condition, l.Vehicle.FuelType, l.Vehicle.Transmission,
            l.Department.Name, l.Province.Name, l.District.Name, l.PublishedAt,
            l.Operations.Where(o => o.IsActive).Select(o => new PublicOperationSummaryDto(o.OperationType, o.Status, o.Currency,
                o.SalePrice, o.DailyRate, o.WeeklyRate, o.MonthlyRate, o.IsNegotiable)).ToList())).ToListAsync(ct);
        return new PagedResult<ListingSummaryDto>(items, page, pageSize, total);
    }

    private static void Validate(ListingSearchQuery s)
    {
        var errors = new Dictionary<string, string[]>();
        if ((s.PriceMin is not null || s.PriceMax is not null || s.Sort is ListingSort.PriceAsc or ListingSort.PriceDesc) && s.Currency is null)
            errors["currency"] = ["Seleccione la moneda (PEN o USD) para filtrar u ordenar por precio; no se comparan monedas distintas."];
        if (s.PriceMin is < 0 || s.PriceMax is < 0 || s.PriceMin > s.PriceMax) errors["price"] = ["Rango de precio inválido."];
        if (s.YearMin > s.YearMax) errors["year"] = ["Rango de años inválido."];
        if (errors.Count > 0) throw new BusinessValidationException(errors);
    }

    public async Task<ListingDetailDto> GetDetailAsync(Guid id, CancellationToken ct)
    {
        var l = await db.Listings.AsNoTracking().Where(x => x.Id == id && Visible.Contains(x.Status) && !x.Vehicle.IsDeleted).Select(x => new
        {
            x.Id, x.Title, x.Description, x.Status, x.IsFeatured, x.LocationReference, x.AdditionalConditions, x.ContactHours, x.PublishedAt,
            x.SellerId, x.AdvertiserType, x.BusinessName, x.DepartmentId, x.ProvinceId, x.DistrictId,
            HasContact = x.PhoneNumber != null || x.WhatsAppNumber != null,
            Department = x.Department.Name, Province = x.Province.Name, District = x.District.Name,
            Vehicle = x.Vehicle,
            Type = x.Vehicle.VehicleType.Name, Brand = x.Vehicle.Model.Brand.Name, Model = x.Vehicle.Model.Name,
            Images = x.Vehicle.Images.OrderBy(i => i.SortOrder).Select(i => new VehicleImageDto(i.Id, i.Url, i.SortOrder, i.IsCover)).ToList(),
            Operations = x.Operations.Where(o => o.IsActive).AsQueryable().Select(ListingService.OperationProjection).ToList()
        }).FirstOrDefaultAsync(ct) ?? throw new NotFoundException("Publicación no encontrada.");

        var v = l.Vehicle;
        var vehicle = new PublicVehicleDto(l.Type, l.Brand, l.Model, v.Version, v.ManufactureYear, v.ModelYear, PlateNormalizer.Mask(v.Plate),
            v.Color, v.Mileage, v.Condition, v.PreviousOwners, v.FuelType, v.Transmission, v.EngineDisplacementCc, v.Doors, v.Seats, v.Traction,
            v.LoadCapacityKg, v.HasAirConditioning, v.HasGps, v.HasInsurance, v.HasValidSoat, v.HasValidTechnicalReview, v.Description);
        // La titularidad legal declarada NO se publica ni se presenta como verificada.

        return new ListingDetailDto(l.Id, l.Title, l.Description, l.Status == ListingStatus.Published, l.Status, l.IsFeatured,
            l.Department ?? "", l.Province ?? "", l.District ?? "", l.LocationReference, l.AdditionalConditions, l.ContactHours, l.PublishedAt,
            vehicle, l.Images, l.Operations, await AdvertiserAsync(l.SellerId, l.AdvertiserType, l.BusinessName, ct), l.HasContact);
    }

    public async Task<PublicAdvertiserDto> GetAdvertiserAsync(Guid listingId, CancellationToken ct)
    {
        var l = await db.Listings.AsNoTracking().Where(x => x.Id == listingId && Visible.Contains(x.Status))
            .Select(x => new { x.SellerId, x.AdvertiserType, x.BusinessName }).FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Publicación no encontrada.");
        return await AdvertiserAsync(l.SellerId, l.AdvertiserType, l.BusinessName, ct);
    }

    private async Task<PublicAdvertiserDto> AdvertiserAsync(Guid sellerId, ProfileType type, string? businessName, CancellationToken ct)
    {
        var u = await db.Users.AsNoTracking().Where(x => x.Id == sellerId)
            .Select(x => new { x.CreatedAt, First = x.Profile != null ? x.Profile.FirstName : "", Last = x.Profile != null ? x.Profile.LastName : "" }).FirstAsync(ct);
        var active = await db.Listings.CountAsync(x => x.SellerId == sellerId && x.Status == ListingStatus.Published, ct);
        // Solo nombre y inicial del apellido: no se expone información personal innecesaria.
        var name = type == ProfileType.Business && !string.IsNullOrWhiteSpace(businessName) ? businessName
            : $"{u.First} {(u.Last.Length > 0 ? u.Last[0] + "." : "")}".Trim();
        return new PublicAdvertiserDto(sellerId, name, type, u.CreatedAt, active);
    }

    public async Task RegisterViewAsync(Guid listingId, Guid? viewerId, CancellationToken ct)
    {
        var l = await db.Listings.AsNoTracking().Where(x => x.Id == listingId && Visible.Contains(x.Status)).Select(x => new { x.SellerId }).FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Publicación no encontrada.");
        if (viewerId == l.SellerId) return; // las visitas del propio anunciante no cuentan
        var now = clock.GetUtcNow().UtcDateTime;
        if (viewerId is not null && await db.ListingViews.AnyAsync(v => v.ListingId == listingId && v.ViewerId == viewerId && v.ViewedAt > now.AddMinutes(-30), ct)) return;
        db.ListingViews.Add(new ListingView { ListingId = listingId, ViewerId = viewerId, ViewedAt = now });
        await db.SaveChangesAsync(ct);
    }

    public async Task<ContactInfoDto> RegisterContactAsync(Guid listingId, Guid? userId, ContactChannel channel, CancellationToken ct)
    {
        var l = await db.Listings.AsNoTracking().Where(x => x.Id == listingId && x.Status == ListingStatus.Published)
            .Select(x => new { x.Title, x.SellerId, x.PhoneNumber, x.WhatsAppNumber, x.ContactHours }).FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Publicación no disponible.");
        if (userId == l.SellerId) throw new ConflictException("No puedes contactarte a ti mismo.");

        string? phone = null, wa = null;
        switch (channel)
        {
            case ContactChannel.Phone when l.PhoneNumber is not null: phone = l.PhoneNumber; break;
            case ContactChannel.WhatsApp when (l.WhatsAppNumber ?? l.PhoneNumber) is { } n:
                wa = WhatsAppLink(n, $"Hola, me interesa tu publicación en MotorLink: {l.Title}"); break;
            default: throw new ConflictException("El anunciante no ofrece ese canal de contacto.");
        }

        db.ContactEvents.Add(new ContactEvent { ListingId = listingId, UserId = userId, Channel = channel, CreatedAt = clock.GetUtcNow().UtcDateTime });
        await db.SaveChangesAsync(ct);
        return new ContactInfoDto(channel, phone, wa, l.ContactHours,
            "Se registró tu clic de contacto. Esto no confirma un acuerdo ni una venta/alquiler.");
    }

    public static string WhatsAppLink(string number, string message)
    {
        var digits = new string(number.Where(char.IsDigit).ToArray());
        if (digits.Length == 9 && digits[0] == '9') digits = "51" + digits; // celular peruano sin prefijo
        return $"https://wa.me/{digits}?text={Uri.EscapeDataString(message)}";
    }
}

public sealed record FavoriteDto(Guid ListingId, DateTime AddedAt, ListingSummaryDto? Listing);

public interface IFavoriteService
{
    Task<bool> AddAsync(Guid userId, Guid listingId, CancellationToken ct);
    Task RemoveAsync(Guid userId, Guid listingId, CancellationToken ct);
    Task<PagedResult<ListingSummaryDto>> ListAsync(Guid userId, int page, int pageSize, CancellationToken ct);
}

public sealed class FavoriteService(IApplicationDbContext db, TimeProvider clock) : IFavoriteService
{
    /// <summary>Idempotente: devuelve true si se creó, false si ya existía (sin duplicados).</summary>
    public async Task<bool> AddAsync(Guid userId, Guid listingId, CancellationToken ct)
    {
        if (!await db.Listings.AnyAsync(l => l.Id == listingId && ListingStateMachine.PublicVisible.Contains(l.Status), ct))
            throw new NotFoundException("Publicación no encontrada.");
        if (await db.Favorites.AnyAsync(f => f.UserId == userId && f.ListingId == listingId, ct)) return false;
        db.Favorites.Add(new Favorite { UserId = userId, ListingId = listingId, CreatedAt = clock.GetUtcNow().UtcDateTime });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { return false; } // creado en paralelo por otra solicitud
        return true;
    }

    public async Task RemoveAsync(Guid userId, Guid listingId, CancellationToken ct)
    {
        var f = await db.Favorites.FirstOrDefaultAsync(x => x.UserId == userId && x.ListingId == listingId, ct)
            ?? throw new NotFoundException("La publicación no está en tus favoritos.");
        db.Favorites.Remove(f);
        await db.SaveChangesAsync(ct);
    }

    public async Task<PagedResult<ListingSummaryDto>> ListAsync(Guid userId, int page, int pageSize, CancellationToken ct)
    {
        (page, pageSize) = Paging.Normalize(page, pageSize);
        var visible = ListingStateMachine.PublicVisible;
        var q = db.Favorites.AsNoTracking().Where(f => f.UserId == userId && visible.Contains(f.Listing.Status)).OrderByDescending(f => f.CreatedAt);
        var total = await q.CountAsync(ct);
        var items = await q.Skip((page - 1) * pageSize).Take(pageSize).Select(f => new ListingSummaryDto(
            f.Listing.Id, f.Listing.Title, f.Listing.Status == ListingStatus.Published, f.Listing.Status, f.Listing.IsFeatured,
            f.Listing.Vehicle.Images.OrderByDescending(i => i.IsCover).ThenBy(i => i.SortOrder).Select(i => i.Url).FirstOrDefault(),
            f.Listing.Vehicle.VehicleType.Name, f.Listing.Vehicle.Model.Brand.Name, f.Listing.Vehicle.Model.Name, f.Listing.Vehicle.Version,
            f.Listing.Vehicle.ModelYear, f.Listing.Vehicle.Mileage, f.Listing.Vehicle.Condition, f.Listing.Vehicle.FuelType, f.Listing.Vehicle.Transmission,
            f.Listing.Department.Name, f.Listing.Province.Name, f.Listing.District.Name, f.Listing.PublishedAt,
            f.Listing.Operations.Where(o => o.IsActive).Select(o => new PublicOperationSummaryDto(o.OperationType, o.Status, o.Currency,
                o.SalePrice, o.DailyRate, o.WeeklyRate, o.MonthlyRate, o.IsNegotiable)).ToList())).ToListAsync(ct);
        return new PagedResult<ListingSummaryDto>(items, page, pageSize, total);
    }
}
