using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using MotorLink.Application.Common;
using MotorLink.Domain.Common;
using MotorLink.Domain.Entities;
using MotorLink.Domain.Exceptions;

namespace MotorLink.Application.Subscriptions;

public static class PlanCodes
{
    public const string Free = "FREE";
}

public sealed record PlanDto(
    int Id, string Code, string Name, decimal MonthlyPrice, int MaxActiveListings, int MaxPhotosPerVehicle,
    int MaxFeaturedListings, bool HasAdvancedStats, bool HasBusinessProfile, bool CanExportReports,
    bool HasPrioritySupport, int SortOrder, bool IsActive)
{
    public static PlanDto From(Plan p) => new(p.Id, p.Code, p.Name, p.MonthlyPrice, p.MaxActiveListings, p.MaxPhotosPerVehicle,
        p.MaxFeaturedListings, p.HasAdvancedStats, p.HasBusinessProfile, p.CanExportReports, p.HasPrioritySupport, p.SortOrder, p.IsActive);
}

public sealed record PlanRequest(
    [Required, RegularExpression("^[A-Z][A-Z0-9_]{1,29}$", ErrorMessage = "Código en MAYÚSCULAS.")] string Code,
    [Required, StringLength(60)] string Name,
    [Range(0, 1_000_000)] decimal MonthlyPrice,
    [Range(0, 100_000)] int MaxActiveListings,
    [Range(0, 1_000)] int MaxPhotosPerVehicle,
    [Range(0, 100_000)] int MaxFeaturedListings,
    bool HasAdvancedStats, bool HasBusinessProfile, bool CanExportReports, bool HasPrioritySupport,
    int SortOrder = 0);

public interface IPlanService
{
    Task<IReadOnlyList<PlanDto>> ListActiveAsync(CancellationToken ct);
    Task<IReadOnlyList<PlanDto>> ListAllAsync(CancellationToken ct);
    Task<PlanDto> UpsertAsync(int? id, PlanRequest r, CancellationToken ct);
    Task SetActiveAsync(int id, bool active, CancellationToken ct);
    Task DeleteAsync(int id, CancellationToken ct);
}

public sealed class PlanService(IApplicationDbContext db, IAuditService audit) : IPlanService
{
    public async Task<IReadOnlyList<PlanDto>> ListActiveAsync(CancellationToken ct) =>
        (await db.Plans.AsNoTracking().Where(p => p.IsActive).OrderBy(p => p.SortOrder).ThenBy(p => p.MonthlyPrice).ToListAsync(ct)).Select(PlanDto.From).ToList();

    public async Task<IReadOnlyList<PlanDto>> ListAllAsync(CancellationToken ct) =>
        (await db.Plans.AsNoTracking().OrderBy(p => p.SortOrder).ThenBy(p => p.MonthlyPrice).ToListAsync(ct)).Select(PlanDto.From).ToList();

    public async Task<PlanDto> UpsertAsync(int? id, PlanRequest r, CancellationToken ct)
    {
        Plan e;
        if (id is null) e = new Plan { Id = ((await db.Plans.MaxAsync(p => (int?)p.Id, ct)) ?? 0) + 1 };
        else e = await db.Plans.FindAsync([id.Value], ct) ?? throw new NotFoundException("Plan no encontrado.");

        if (e.Code == PlanCodes.Free && r.Code != PlanCodes.Free) throw new ConflictException("El código del plan gratuito no puede cambiar.");
        if (e.Code == PlanCodes.Free && r.MonthlyPrice != 0) throw new ConflictException("El plan gratuito debe costar 0.");
        if (await db.Plans.AnyAsync(p => p.Code == r.Code && p.Id != e.Id, ct)) throw new ConflictException($"Ya existe el plan {r.Code}.");

        e.Code = r.Code; e.Name = r.Name.Trim(); e.MonthlyPrice = r.MonthlyPrice;
        e.MaxActiveListings = r.MaxActiveListings; e.MaxPhotosPerVehicle = r.MaxPhotosPerVehicle; e.MaxFeaturedListings = r.MaxFeaturedListings;
        e.HasAdvancedStats = r.HasAdvancedStats; e.HasBusinessProfile = r.HasBusinessProfile;
        e.CanExportReports = r.CanExportReports; e.HasPrioritySupport = r.HasPrioritySupport; e.SortOrder = r.SortOrder;
        if (id is null) db.Plans.Add(e);

        audit.Log(id is null ? "plan.create" : "plan.update", nameof(Plan), e.Id.ToString(), $"{e.Code} S/{e.MonthlyPrice}");
        await db.SaveChangesAsync(ct);
        return PlanDto.From(e);
    }

    public async Task SetActiveAsync(int id, bool active, CancellationToken ct)
    {
        var e = await db.Plans.FindAsync([id], ct) ?? throw new NotFoundException("Plan no encontrado.");
        if (!active && e.Code == PlanCodes.Free) throw new ConflictException("El plan gratuito no puede desactivarse.");
        e.IsActive = active;
        audit.Log(active ? "plan.activate" : "plan.deactivate", nameof(Plan), id.ToString());
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var e = await db.Plans.FindAsync([id], ct) ?? throw new NotFoundException("Plan no encontrado.");
        if (e.Code == PlanCodes.Free) throw new ConflictException("El plan gratuito no puede eliminarse.");
        if (await db.Subscriptions.AnyAsync(s => s.PlanId == id, ct))
            throw new ConflictException("El plan tiene suscripciones asociadas; desactívelo en su lugar.");
        db.Plans.Remove(e);
        audit.Log("plan.delete", nameof(Plan), id.ToString());
        await db.SaveChangesAsync(ct);
    }
}
