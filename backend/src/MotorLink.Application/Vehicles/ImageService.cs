using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MotorLink.Application.Common;
using MotorLink.Application.Subscriptions;
using MotorLink.Domain.Entities;
using MotorLink.Domain.Exceptions;

namespace MotorLink.Application.Vehicles;

public sealed record StoredImage(string Url, string PublicId);

/// <summary>Abstracción del proveedor de imágenes (Cloudinary). La aplicación nunca depende del SDK concreto.</summary>
public interface IImageStorageService
{
    Task<StoredImage> UploadAsync(Stream content, string contentType, string folder, CancellationToken ct);
    Task DeleteAsync(string publicId, CancellationToken ct);
}

public static class ImageSignature
{
    public const long MaxBytes = 5 * 1024 * 1024;

    /// <summary>Detecta el formato real por los bytes iniciales (no por extensión ni Content-Type).</summary>
    public static string? Detect(ReadOnlySpan<byte> h)
    {
        if (h.Length >= 3 && h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF) return "image/jpeg";
        if (h.Length >= 8 && h[0] == 0x89 && h[1] == 0x50 && h[2] == 0x4E && h[3] == 0x47 && h[4] == 0x0D && h[5] == 0x0A && h[6] == 0x1A && h[7] == 0x0A) return "image/png";
        if (h.Length >= 12 && h[0] == 'R' && h[1] == 'I' && h[2] == 'F' && h[3] == 'F' && h[8] == 'W' && h[9] == 'E' && h[10] == 'B' && h[11] == 'P') return "image/webp";
        return null;
    }
}

public interface IVehicleImageService
{
    Task<IReadOnlyList<VehicleImageDto>> ListAsync(Guid userId, Guid vehicleId, CancellationToken ct);
    Task<VehicleImageDto> UploadAsync(Guid userId, Guid vehicleId, Stream content, string declaredContentType, CancellationToken ct);
    Task DeleteAsync(Guid userId, Guid vehicleId, Guid imageId, CancellationToken ct);
    Task<IReadOnlyList<VehicleImageDto>> ReorderAsync(Guid userId, Guid vehicleId, IReadOnlyList<Guid> orderedIds, CancellationToken ct);
    Task<IReadOnlyList<VehicleImageDto>> SetCoverAsync(Guid userId, Guid vehicleId, Guid imageId, CancellationToken ct);
}

public sealed class VehicleImageService(
    IApplicationDbContext db, IImageStorageService storage, ISubscriptionPermissions permissions,
    ILogger<VehicleImageService> logger, TimeProvider clock) : IVehicleImageService
{
    public async Task<IReadOnlyList<VehicleImageDto>> ListAsync(Guid userId, Guid vehicleId, CancellationToken ct)
    {
        await EnsureOwner(userId, vehicleId, ct);
        return await Images(vehicleId).ToListAsync(ct);
    }

    public async Task<VehicleImageDto> UploadAsync(Guid userId, Guid vehicleId, Stream content, string declaredContentType, CancellationToken ct)
    {
        await EnsureOwner(userId, vehicleId, ct);
        // 1) Comprobación temprana del límite del plan (antes de leer el archivo ni subirlo).
        await permissions.EnsureCanAddPhotoAsync(userId, vehicleId, ct);

        // 2) Lectura acotada: nunca se leen más de 5 MB + 1 byte.
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await content.ReadAsync(chunk, ct)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length > ImageSignature.MaxBytes) throw BusinessValidationException.For("file", "La imagen supera el máximo de 5 MB.");
        }
        if (buffer.Length == 0) throw BusinessValidationException.For("file", "El archivo está vacío.");

        // 3) Contenido real por firma de bytes; debe coincidir con lo declarado.
        var detected = ImageSignature.Detect(buffer.GetBuffer().AsSpan(0, (int)Math.Min(buffer.Length, 16)));
        if (detected is null) throw BusinessValidationException.For("file", "Formato no permitido. Use JPEG, PNG o WebP.");
        if (!string.Equals(detected, declaredContentType, StringComparison.OrdinalIgnoreCase))
            throw BusinessValidationException.For("file", "El contenido del archivo no coincide con su tipo declarado.");

        // 4) Subida al proveedor (fuera de la transacción de BD).
        buffer.Position = 0;
        var stored = await storage.UploadAsync(buffer, detected, $"motorlink/vehicles/{vehicleId}", ct);

        // 5) Registro atómico con verificación definitiva del límite; si falla, se compensa borrando el archivo del proveedor.
        try
        {
            return await db.ExecuteInTransactionAsync(userId, async () =>
            {
                await permissions.EnsureCanAddPhotoAsync(userId, vehicleId, ct);
                var existing = await db.VehicleImages.Where(i => i.VehicleId == vehicleId).Select(i => i.SortOrder).ToListAsync(ct);
                var img = new VehicleImage
                {
                    Id = Guid.NewGuid(), VehicleId = vehicleId, Url = stored.Url, ProviderPublicId = stored.PublicId,
                    ContentType = detected, SizeBytes = buffer.Length,
                    SortOrder = existing.Count == 0 ? 0 : existing.Max() + 1, IsCover = existing.Count == 0
                };
                db.VehicleImages.Add(img);
                await db.SaveChangesAsync(ct);
                return new VehicleImageDto(img.Id, img.Url, img.SortOrder, img.IsCover);
            }, ct);
        }
        catch
        {
            await TryDeleteFromProviderAsync(stored.PublicId);
            throw;
        }
    }

    public async Task DeleteAsync(Guid userId, Guid vehicleId, Guid imageId, CancellationToken ct)
    {
        await EnsureOwner(userId, vehicleId, ct);
        var all = await db.VehicleImages.Where(i => i.VehicleId == vehicleId).OrderBy(i => i.SortOrder).ToListAsync(ct);
        var img = all.FirstOrDefault(i => i.Id == imageId) ?? throw new NotFoundException("Fotografía no encontrada.");

        db.VehicleImages.Remove(img);
        if (img.IsCover) all.Where(i => i.Id != imageId).FirstOrDefault()?.SetCover();
        // Se registra el archivo como pendiente de borrado en la MISMA transacción que la fila: nunca queda huérfano sin rastro.
        db.OrphanedImages.Add(new OrphanedImage { ProviderPublicId = img.ProviderPublicId, CreatedAt = clock.GetUtcNow().UtcDateTime });
        await db.SaveChangesAsync(ct);

        // Borrado inmediato; si falla, el proceso periódico de limpieza lo reintenta.
        try
        {
            await storage.DeleteAsync(img.ProviderPublicId, ct);
            var pending = await db.OrphanedImages.FirstOrDefaultAsync(o => o.ProviderPublicId == img.ProviderPublicId, ct);
            if (pending is not null) { db.OrphanedImages.Remove(pending); await db.SaveChangesAsync(ct); }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "No se pudo borrar {PublicId} del proveedor; quedará para reintento.", img.ProviderPublicId);
        }
    }

    public async Task<IReadOnlyList<VehicleImageDto>> ReorderAsync(Guid userId, Guid vehicleId, IReadOnlyList<Guid> orderedIds, CancellationToken ct)
    {
        await EnsureOwner(userId, vehicleId, ct);
        var all = await db.VehicleImages.Where(i => i.VehicleId == vehicleId).ToListAsync(ct);
        if (orderedIds.Count != all.Count || orderedIds.Distinct().Count() != all.Count || all.Any(i => !orderedIds.Contains(i.Id)))
            throw BusinessValidationException.For("imageIds", "Debe enviar exactamente todas las fotografías del vehículo, sin repetidas.");
        for (var i = 0; i < orderedIds.Count; i++) all.First(x => x.Id == orderedIds[i]).SortOrder = i;
        await db.SaveChangesAsync(ct);
        return await Images(vehicleId).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<VehicleImageDto>> SetCoverAsync(Guid userId, Guid vehicleId, Guid imageId, CancellationToken ct)
    {
        await EnsureOwner(userId, vehicleId, ct);
        var all = await db.VehicleImages.Where(i => i.VehicleId == vehicleId).ToListAsync(ct);
        if (all.All(i => i.Id != imageId)) throw new NotFoundException("Fotografía no encontrada.");
        foreach (var i in all) i.IsCover = i.Id == imageId;
        await db.SaveChangesAsync(ct);
        return await Images(vehicleId).ToListAsync(ct);
    }

    private IQueryable<VehicleImageDto> Images(Guid vehicleId) => db.VehicleImages.AsNoTracking()
        .Where(i => i.VehicleId == vehicleId).OrderBy(i => i.SortOrder).Select(i => new VehicleImageDto(i.Id, i.Url, i.SortOrder, i.IsCover));

    private async Task EnsureOwner(Guid userId, Guid vehicleId, CancellationToken ct)
    {
        if (!await db.Vehicles.AnyAsync(v => v.Id == vehicleId && v.OwnerId == userId, ct)) throw new NotFoundException("Vehículo no encontrado.");
    }

    private async Task TryDeleteFromProviderAsync(string publicId)
    {
        try { await storage.DeleteAsync(publicId, CancellationToken.None); }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudo compensar la subida de {PublicId}; se registra para limpieza.", publicId);
            try
            {
                db.OrphanedImages.Add(new OrphanedImage { ProviderPublicId = publicId, CreatedAt = clock.GetUtcNow().UtcDateTime, LastError = ex.Message });
                await db.SaveChangesAsync(CancellationToken.None);
            }
            catch (Exception inner) { logger.LogError(inner, "No se pudo registrar el archivo huérfano {PublicId}.", publicId); }
        }
    }
}

internal static class VehicleImageExtensions
{
    public static void SetCover(this VehicleImage i) => i.IsCover = true;
}

/// <summary>Reintenta el borrado de archivos huérfanos en el proveedor.</summary>
public interface IImageMaintenance
{
    Task<int> CleanOrphansAsync(CancellationToken ct);
}

public sealed class ImageMaintenance(IApplicationDbContext db, IImageStorageService storage, ILogger<ImageMaintenance> logger) : IImageMaintenance
{
    private const int MaxAttempts = 10;

    public async Task<int> CleanOrphansAsync(CancellationToken ct)
    {
        var pending = await db.OrphanedImages.Where(o => o.Attempts < MaxAttempts).OrderBy(o => o.CreatedAt).Take(50).ToListAsync(ct);
        var cleaned = 0;
        foreach (var o in pending)
        {
            try { await storage.DeleteAsync(o.ProviderPublicId, ct); db.OrphanedImages.Remove(o); cleaned++; }
            catch (Exception ex)
            {
                o.Attempts++; o.LastError = ex.Message[..Math.Min(ex.Message.Length, 400)];
                logger.LogWarning("Reintento {Attempt} fallido para {PublicId}", o.Attempts, o.ProviderPublicId);
            }
        }
        await db.SaveChangesAsync(ct);
        return cleaned;
    }
}
