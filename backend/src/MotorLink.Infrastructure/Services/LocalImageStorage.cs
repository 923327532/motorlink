using Microsoft.Extensions.Options;
using MotorLink.Application.Vehicles;

namespace MotorLink.Infrastructure.Services;

public sealed class LocalImageOptions
{
    public const string SectionName = "ImageStorage";
    /// <summary>Usa disco local cuando Cloudinary no está configurado. SOLO desarrollo/demostración.</summary>
    public bool LocalFallback { get; set; }
    public string Path { get; set; } = "App_Data/images";
}

/// <summary>
/// Respaldo de DESARROLLO/DEMO: guarda las imágenes en disco y las expone en /media/{ruta}.
/// No sustituye a Cloudinary en producción (sin CDN, sin transformaciones, un solo servidor).
/// </summary>
public sealed class LocalDiskImageStorage(IOptions<LocalImageOptions> options) : IImageStorageService
{
    public string Root => System.IO.Path.GetFullPath(options.Value.Path);

    public async Task<StoredImage> UploadAsync(Stream content, string contentType, string folder, CancellationToken ct)
    {
        var ext = contentType switch { "image/png" => "png", "image/webp" => "webp", _ => "jpg" };
        var safeFolder = string.Join('/', folder.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Sanitize));
        var publicId = $"{safeFolder}/{Guid.NewGuid():N}.{ext}";
        var full = System.IO.Path.Combine(Root, publicId.Replace('/', System.IO.Path.DirectorySeparatorChar));
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
        await using var file = File.Create(full);
        await content.CopyToAsync(file, ct);
        return new StoredImage("/media/" + publicId, publicId);
    }

    public Task DeleteAsync(string publicId, CancellationToken ct)
    {
        var full = Resolve(publicId);
        if (full is not null && File.Exists(full)) File.Delete(full);
        return Task.CompletedTask;
    }

    /// <summary>Convierte una ruta pública en ruta física validando que no escape del directorio raíz.</summary>
    public string? Resolve(string publicId)
    {
        if (string.IsNullOrWhiteSpace(publicId) || publicId.Contains("..") || publicId.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '/' or '-' or '_' or '.')))
            return null;
        var full = System.IO.Path.GetFullPath(System.IO.Path.Combine(Root, publicId.Replace('/', System.IO.Path.DirectorySeparatorChar)));
        return full.StartsWith(Root, StringComparison.Ordinal) ? full : null;
    }

    private static string Sanitize(string s) => new(s.Where(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_').ToArray());
}
