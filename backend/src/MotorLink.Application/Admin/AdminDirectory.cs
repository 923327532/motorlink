using Microsoft.EntityFrameworkCore;
using MotorLink.Application.Common;
using MotorLink.Application.Vehicles;
using MotorLink.Domain.Common;
using MotorLink.Domain.Rules;

namespace MotorLink.Application.Admin;

public sealed record AdminBusinessDto(Guid Id, Guid UserId, string BusinessName, string? Ruc, string OwnerEmail, bool IsVerified, int ActiveListings, DateTime CreatedAt);
public sealed record AdminVehicleDto(Guid Id, string OwnerEmail, string VehicleType, string Brand, string Model, int ModelYear, string? MaskedPlate,
    VehicleCondition Condition, int Mileage, ListingStatus? ListingStatus, int Photos, DateTime CreatedAt);
public sealed record AdminNegotiationDto(Guid Id, string ListingTitle, OperationType OperationType, string BuyerEmail, string SellerEmail,
    NegotiationStatus Status, Currency Currency, RentPeriod? Periodicity, decimal InitialAmount, decimal LastAmount, DateTime CreatedAt, DateTime? UpdatedAt);

/// <summary>Consultas administrativas de solo lectura. No exponen placas completas ni documentos.</summary>
public interface IAdminDirectoryService
{
    Task<PagedResult<AdminBusinessDto>> BusinessesAsync(string? search, int page, int pageSize, CancellationToken ct);
    Task<PagedResult<AdminVehicleDto>> VehiclesAsync(string? search, int page, int pageSize, CancellationToken ct);
    Task<PagedResult<AdminNegotiationDto>> NegotiationsAsync(NegotiationStatus? status, int page, int pageSize, CancellationToken ct);
}

public sealed class AdminDirectoryService(IApplicationDbContext db) : IAdminDirectoryService
{
    public Task<PagedResult<AdminBusinessDto>> BusinessesAsync(string? search, int page, int pageSize, CancellationToken ct) =>
        db.BusinessProfiles.AsNoTracking()
            .Where(b => string.IsNullOrWhiteSpace(search) || b.BusinessName.Contains(search!) || (b.Ruc != null && b.Ruc.Contains(search!)) || b.User.Email!.Contains(search!))
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => new AdminBusinessDto(b.Id, b.UserId, b.BusinessName, b.Ruc, b.User.Email!, b.IsVerified,
                db.Listings.Count(l => l.SellerId == b.UserId && l.Status == ListingStatus.Published), b.CreatedAt))
            .ToPagedAsync(page, pageSize, ct);

    public async Task<PagedResult<AdminVehicleDto>> VehiclesAsync(string? search, int page, int pageSize, CancellationToken ct)
    {
        (page, pageSize) = Paging.Normalize(page, pageSize);
        var q = db.Vehicles.AsNoTracking().Where(v => string.IsNullOrWhiteSpace(search) || v.Model.Name.Contains(search!) || v.Model.Brand.Name.Contains(search!)
                                                      || v.Owner.Email!.Contains(search!) || (v.Plate != null && v.Plate == search!.Replace("-", "").ToUpper()));
        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(v => v.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).Select(v => new
        {
            v.Id, Owner = v.Owner.Email!, Type = v.VehicleType.Name, Brand = v.Model.Brand.Name, Model = v.Model.Name, v.ModelYear, v.Plate,
            v.Condition, v.Mileage, v.CreatedAt, Photos = v.Images.Count,
            Listing = db.Listings.Where(l => l.VehicleId == v.Id).Select(l => (ListingStatus?)l.Status).FirstOrDefault()
        }).ToListAsync(ct);
        var items = rows.Select(r => new AdminVehicleDto(r.Id, r.Owner, r.Type, r.Brand, r.Model, r.ModelYear, PlateNormalizer.Mask(r.Plate),
            r.Condition, r.Mileage, r.Listing, r.Photos, r.CreatedAt)).ToList();
        return new PagedResult<AdminVehicleDto>(items, page, pageSize, total);
    }

    public async Task<PagedResult<AdminNegotiationDto>> NegotiationsAsync(NegotiationStatus? status, int page, int pageSize, CancellationToken ct)
    {
        (page, pageSize) = Paging.Normalize(page, pageSize);
        var q = db.Negotiations.AsNoTracking().Where(n => status == null || n.Status == status);
        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(n => n.UpdatedAt ?? n.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).Select(n => new
        {
            n.Id, n.Listing.Title, n.ListingOperation.OperationType, Buyer = n.Buyer.Email!, Seller = n.Seller.Email!, n.Status, n.Currency,
            n.Periodicity, n.InitialAmount, n.CreatedAt, n.UpdatedAt,
            Last = n.Offers.OrderByDescending(o => o.Sequence).Select(o => o.Amount).FirstOrDefault()
        }).ToListAsync(ct);
        var items = rows.Select(r => new AdminNegotiationDto(r.Id, r.Title, r.OperationType, r.Buyer, r.Seller, r.Status, r.Currency,
            r.Periodicity, r.InitialAmount, r.Last, r.CreatedAt, r.UpdatedAt)).ToList();
        return new PagedResult<AdminNegotiationDto>(items, page, pageSize, total);
    }
}
