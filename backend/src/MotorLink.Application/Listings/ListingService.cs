using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using MotorLink.Application.Common;
using MotorLink.Application.Subscriptions;
using MotorLink.Domain.Common;
using MotorLink.Domain.Entities;
using MotorLink.Domain.Exceptions;
using MotorLink.Domain.Rules;

namespace MotorLink.Application.Listings;

public interface IListingService
{
    Task<ListingDto> CreateAsync(Guid userId, ListingRequest r, CancellationToken ct);
    Task<PagedResult<ListingDto>> ListMineAsync(Guid userId, ListingStatus? status, int page, int pageSize, CancellationToken ct);
    Task<ListingDto> GetMineAsync(Guid userId, Guid id, CancellationToken ct);
    Task<ListingDto> UpdateAsync(Guid userId, Guid id, ListingRequest r, CancellationToken ct);
    Task<ListingDto> ChangeStatusAsync(Guid userId, Guid id, ChangeListingStatusRequest r, CancellationToken ct);
    Task<ListingDto> SetFeaturedAsync(Guid userId, Guid id, bool featured, CancellationToken ct);
    Task DeleteAsync(Guid userId, Guid id, CancellationToken ct);
}

public sealed record ModerationItemDto(Guid Id, string Title, ListingStatus Status, Guid SellerId, string SellerEmail, string Vehicle, DateTime CreatedAt, DateTime? ReviewedAt, string? RejectionReason);

public interface IListingModerationService
{
    Task<PagedResult<ModerationItemDto>> ListAsync(ListingStatus? status, int page, int pageSize, CancellationToken ct);
    Task ApproveAsync(Guid adminId, Guid id, CancellationToken ct);
    Task RejectAsync(Guid adminId, Guid id, string reason, CancellationToken ct);
}

public sealed partial class ListingService(
    IApplicationDbContext db, IListingLifecycle lifecycle, ISubscriptionPermissions permissions, TimeProvider clock) : IListingService
{
    [GeneratedRegex(@"^\+?\d{7,15}$")] private static partial Regex PhoneRegex();

    public async Task<ListingDto> CreateAsync(Guid userId, ListingRequest r, CancellationToken ct)
    {
        var vehicle = await db.Vehicles.AsNoTracking().FirstOrDefaultAsync(v => v.Id == r.VehicleId && v.OwnerId == userId, ct)
            ?? throw BusinessValidationException.For(nameof(r.VehicleId), "Vehículo no encontrado.");
        if (await db.Listings.AnyAsync(l => l.VehicleId == vehicle.Id, ct))
            throw new ConflictException("El vehículo ya tiene una publicación. Edítela para agregar o cambiar modalidades.");

        await ValidateAsync(userId, r, ct);
        var listing = new Listing { Id = Guid.NewGuid(), VehicleId = vehicle.Id, SellerId = userId, Status = ListingStatus.Draft };
        ApplyHeader(listing, r);
        db.Listings.Add(listing);
        ApplyOperations(listing, r.Operations);
        await db.SaveChangesAsync(ct);
        return await GetMineAsync(userId, listing.Id, ct);
    }

    public async Task<PagedResult<ListingDto>> ListMineAsync(Guid userId, ListingStatus? status, int page, int pageSize, CancellationToken ct)
    {
        (page, pageSize) = Paging.Normalize(page, pageSize);
        var q = db.Listings.AsNoTracking().Where(l => l.SellerId == userId && (status == null || l.Status == status));
        var total = await q.CountAsync(ct);
        var items = await Project(q.OrderByDescending(l => l.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize)).ToListAsync(ct);
        return new PagedResult<ListingDto>(items, page, pageSize, total);
    }

    public async Task<ListingDto> GetMineAsync(Guid userId, Guid id, CancellationToken ct) =>
        await Project(db.Listings.AsNoTracking().Where(l => l.Id == id && l.SellerId == userId)).FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException("Publicación no encontrada.");

    public async Task<ListingDto> UpdateAsync(Guid userId, Guid id, ListingRequest r, CancellationToken ct)
    {
        var l = await db.Listings.Include(x => x.Operations).ThenInclude(o => o.WorkRent)
            .FirstOrDefaultAsync(x => x.Id == id && x.SellerId == userId, ct) ?? throw new NotFoundException("Publicación no encontrada.");
        if (!ListingStateMachine.Editable.Contains(l.Status))
            throw new ConflictException($"Una publicación en estado {l.Status} no puede editarse. Páusela primero.");
        if (r.VehicleId != l.VehicleId) throw BusinessValidationException.For(nameof(r.VehicleId), "No se puede cambiar el vehículo de una publicación.");

        await ValidateAsync(userId, r, ct);
        ApplyHeader(l, r);

        // Las modalidades que ya no se envían se desactivan si tienen negociaciones, o se eliminan si no.
        var incoming = r.Operations.Select(o => o.OperationType).ToHashSet();
        foreach (var op in l.Operations.Where(o => !incoming.Contains(o.OperationType)).ToList())
        {
            if (await db.Negotiations.AnyAsync(n => n.ListingOperationId == op.Id, ct)) { op.IsActive = false; op.Status = OperationStatus.Closed; }
            else db.ListingOperations.Remove(op);
        }
        ApplyOperations(l, r.Operations);
        if (l.Status == ListingStatus.Rejected) { l.Status = ListingStatus.Draft; }
        await db.SaveChangesAsync(ct);
        return await GetMineAsync(userId, id, ct);
    }

    public async Task<ListingDto> ChangeStatusAsync(Guid userId, Guid id, ChangeListingStatusRequest r, CancellationToken ct)
    {
        await lifecycle.TransitionAsync(id, r.Status, r.OperationType, userId, asModerator: false, note: null, ct);
        return await GetMineAsync(userId, id, ct);
    }

    public async Task<ListingDto> SetFeaturedAsync(Guid userId, Guid id, bool featured, CancellationToken ct)
    {
        await db.ExecuteInTransactionAsync(userId, async () =>
        {
            var l = await db.Listings.FirstOrDefaultAsync(x => x.Id == id && x.SellerId == userId, ct) ?? throw new NotFoundException("Publicación no encontrada.");
            if (featured)
            {
                if (l.Status != ListingStatus.Published) throw new ConflictException("Solo las publicaciones PUBLISHED pueden destacarse.");
                if (!l.IsFeatured) await permissions.EnsureCanFeatureAsync(userId, id, ct);
            }
            l.IsFeatured = featured;
            await db.SaveChangesAsync(ct);
            return true;
        }, ct);
        return await GetMineAsync(userId, id, ct);
    }

    public async Task DeleteAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var l = await db.Listings.FirstOrDefaultAsync(x => x.Id == id && x.SellerId == userId, ct) ?? throw new NotFoundException("Publicación no encontrada.");
        if (l.Status is not (ListingStatus.Draft or ListingStatus.Rejected or ListingStatus.Archived))
            throw new ConflictException("Solo se pueden eliminar publicaciones en borrador, rechazadas o archivadas.");
        db.Listings.Remove(l); // borrado lógico
        await db.SaveChangesAsync(ct);
    }

    // ---------------- validaciones y mapeo ----------------
    private async Task ValidateAsync(Guid userId, ListingRequest r, CancellationToken ct)
    {
        var errors = new Dictionary<string, List<string>>();
        void Err(string f, string m) { if (!errors.TryGetValue(f, out var l)) errors[f] = l = []; l.Add(m); }

        var d = await db.Districts.AsNoTracking().Where(x => x.Id == r.DistrictId)
            .Select(x => new { x.IsActive, x.ProvinceId, ProvActive = x.Province.IsActive, x.Province.DepartmentId, DepActive = x.Province.Department.IsActive }).FirstOrDefaultAsync(ct);
        if (d is null) Err(nameof(r.DistrictId), "Distrito inexistente.");
        else
        {
            if (d.ProvinceId != r.ProvinceId) Err(nameof(r.ProvinceId), "La provincia no corresponde al distrito.");
            if (d.DepartmentId != r.DepartmentId) Err(nameof(r.DepartmentId), "El departamento no corresponde a la provincia.");
            if (!d.IsActive || !d.ProvActive || !d.DepActive) Err(nameof(r.DistrictId), "La ubicación seleccionada está inactiva.");
        }

        foreach (var (field, value) in new[] { (nameof(r.PhoneNumber), r.PhoneNumber), (nameof(r.WhatsAppNumber), r.WhatsAppNumber) })
            if (!string.IsNullOrWhiteSpace(value) && !PhoneRegex().IsMatch(value.Replace(" ", "").Replace("-", "")))
                Err(field, "Número inválido (7 a 15 dígitos, puede iniciar con +).");

        if (r.AdvertiserType == ProfileType.Business)
        {
            if (string.IsNullOrWhiteSpace(r.BusinessName)) Err(nameof(r.BusinessName), "El nombre comercial es obligatorio para anunciantes BUSINESS.");
            await permissions.RequireFeatureAsync(userId, PlanFeature.BusinessProfile, ct);
        }

        if (r.Operations.Select(o => o.OperationType).Distinct().Count() != r.Operations.Count)
            Err(nameof(r.Operations), "No puede repetir una modalidad.");

        for (var i = 0; i < r.Operations.Count; i++)
        {
            var o = r.Operations[i]; var p = $"Operations[{i}]";
            var rates = new[] { o.DailyRate, o.WeeklyRate, o.MonthlyRate };
            switch (o.OperationType)
            {
                case OperationType.Sale:
                    if (o.SalePrice is null or <= 0) Err($"{p}.SalePrice", "El precio de venta es obligatorio y mayor a cero.");
                    if (rates.Any(x => x is not null)) Err(p, "La venta no admite tarifas de alquiler.");
                    if (o.WorkRent is not null) Err($"{p}.WorkRent", "Las condiciones de trabajo solo aplican a WORK_RENT.");
                    break;
                case OperationType.Rent:
                case OperationType.WorkRent:
                    if (o.SalePrice is not null) Err($"{p}.SalePrice", "El alquiler no admite precio de venta.");
                    if (rates.All(x => x is null)) Err(p, "Indique al menos una tarifa (diaria, semanal o mensual).");
                    if (o.OperationType == OperationType.WorkRent && o.WorkRent is null) Err($"{p}.WorkRent", "Las condiciones de trabajo son obligatorias para WORK_RENT.");
                    if (o.OperationType == OperationType.Rent && o.WorkRent is not null) Err($"{p}.WorkRent", "Las condiciones de trabajo solo aplican a WORK_RENT.");
                    break;
            }
            if (o.AvailableFrom is { } af && af.Date < clock.GetUtcNow().UtcDateTime.Date.AddYears(-1)) Err($"{p}.AvailableFrom", "Fecha de disponibilidad inválida.");
        }

        if (errors.Count > 0) throw new BusinessValidationException(errors.ToDictionary(k => k.Key, v => v.Value.ToArray()));
    }

    private static void ApplyHeader(Listing l, ListingRequest r)
    {
        l.Title = r.Title.Trim(); l.Description = r.Description?.Trim(); l.AdvertiserType = r.AdvertiserType;
        l.BusinessName = r.AdvertiserType == ProfileType.Business ? r.BusinessName?.Trim() : null;
        l.PhoneNumber = Clean(r.PhoneNumber); l.WhatsAppNumber = Clean(r.WhatsAppNumber); l.ContactHours = r.ContactHours?.Trim();
        l.DepartmentId = r.DepartmentId; l.ProvinceId = r.ProvinceId; l.DistrictId = r.DistrictId;
        l.LocationReference = r.LocationReference?.Trim(); l.AdditionalConditions = r.AdditionalConditions?.Trim();
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Replace(" ", "").Replace("-", "");

    private static void ApplyOperations(Listing l, IEnumerable<ListingOperationRequest> requests)
    {
        foreach (var r in requests)
        {
            var op = l.Operations.FirstOrDefault(o => o.OperationType == r.OperationType);
            if (op is null)
            {
                op = new ListingOperation { Id = Guid.NewGuid(), ListingId = l.Id, OperationType = r.OperationType };
                l.Operations.Add(op);
            }
            op.IsActive = true; op.Status = OperationStatus.Available; op.Currency = r.Currency;
            op.SalePrice = r.SalePrice; op.DailyRate = r.DailyRate; op.WeeklyRate = r.WeeklyRate; op.MonthlyRate = r.MonthlyRate;
            op.ReferencePrice = r.OperationType == OperationType.Sale
                ? r.SalePrice!.Value
                : Math.Round(r.DailyRate ?? (r.WeeklyRate.HasValue ? r.WeeklyRate.Value / 7m : r.MonthlyRate!.Value / 30m), 2);
            op.Guarantee = r.Guarantee; op.IsNegotiable = r.IsNegotiable; op.AvailableFrom = r.AvailableFrom;
            op.AdditionalConditions = r.AdditionalConditions?.Trim();

            if (r.OperationType == OperationType.WorkRent && r.WorkRent is { } w)
            {
                var wr = op.WorkRent ??= new WorkRentCondition { ListingOperationId = op.Id };
                wr.AllowedUse = w.AllowedUse; wr.Shift = w.Shift; wr.MinExperienceYears = w.MinExperienceYears;
                wr.LicenseType = w.LicenseType?.Trim(); wr.WorkZone = w.WorkZone?.Trim(); wr.MonthlyKmLimit = w.MonthlyKmLimit;
                wr.FuelPaidBy = w.FuelPaidBy; wr.MaintenancePaidBy = w.MaintenancePaidBy; wr.InsurancePaidBy = w.InsurancePaidBy;
                wr.DriverRequirements = w.DriverRequirements?.Trim();
            }
        }
    }

    internal static System.Linq.Expressions.Expression<Func<ListingOperation, OperationDto>> OperationProjection => o => new OperationDto(
        o.Id, o.OperationType, o.Status, o.IsActive, o.Currency, o.SalePrice, o.DailyRate, o.WeeklyRate, o.MonthlyRate, o.Guarantee,
        o.IsNegotiable, o.AvailableFrom, o.AdditionalConditions,
        o.WorkRent == null ? null : new WorkRentDto(o.WorkRent.AllowedUse, o.WorkRent.Shift, o.WorkRent.MinExperienceYears, o.WorkRent.LicenseType,
            o.WorkRent.WorkZone, o.WorkRent.MonthlyKmLimit, o.WorkRent.FuelPaidBy, o.WorkRent.MaintenancePaidBy, o.WorkRent.InsurancePaidBy,
            o.WorkRent.DriverRequirements));

    private static IQueryable<ListingDto> Project(IQueryable<Listing> q) => q.Select(l => new ListingDto(
        l.Id, l.VehicleId, l.Title, l.Description, l.Status, l.IsFeatured, l.AdvertiserType, l.BusinessName, l.PhoneNumber, l.WhatsAppNumber,
        l.ContactHours, l.DepartmentId, l.ProvinceId, l.DistrictId, l.LocationReference, l.AdditionalConditions, l.RejectionReason,
        l.PublishedAt, l.CreatedAt, l.UpdatedAt,
        l.Vehicle.Model.Brand.Name + " " + l.Vehicle.Model.Name + " " + l.Vehicle.ModelYear,
        l.Vehicle.Images.OrderByDescending(i => i.IsCover).ThenBy(i => i.SortOrder).Select(i => i.Url).FirstOrDefault(),
        l.Vehicle.Images.Count,
        l.Operations.OrderBy(o => o.OperationType).AsQueryable().Select(OperationProjection).ToList()));
}

public sealed class ListingModerationService(IApplicationDbContext db, IListingLifecycle lifecycle) : IListingModerationService
{
    public Task<PagedResult<ModerationItemDto>> ListAsync(ListingStatus? status, int page, int pageSize, CancellationToken ct) =>
        db.Listings.AsNoTracking().Where(l => l.Status == (status ?? ListingStatus.PendingReview))
            .OrderBy(l => l.UpdatedAt ?? l.CreatedAt)
            .Select(l => new ModerationItemDto(l.Id, l.Title, l.Status, l.SellerId, l.Seller.Email!,
                l.Vehicle.Model.Brand.Name + " " + l.Vehicle.Model.Name + " " + l.Vehicle.ModelYear, l.CreatedAt, l.ReviewedAt, l.RejectionReason))
            .ToPagedAsync(page, pageSize, ct);

    public Task ApproveAsync(Guid adminId, Guid id, CancellationToken ct) =>
        lifecycle.TransitionAsync(id, ListingStatus.Published, null, adminId, asModerator: true, note: null, ct);

    public Task RejectAsync(Guid adminId, Guid id, string reason, CancellationToken ct) =>
        lifecycle.TransitionAsync(id, ListingStatus.Rejected, null, adminId, asModerator: true, note: reason, ct);
}
