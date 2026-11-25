using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Options;
using MotorLink.Application.Subscriptions;
using MotorLink.Application.Vehicles;

namespace MotorLink.Infrastructure.Services;

public sealed class CloudinaryOptions
{
    public const string SectionName = "Cloudinary";
    public string CloudName { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ApiSecret { get; set; } = string.Empty;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(CloudName) && !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(ApiSecret);
}

/// <summary>Implementación sobre Cloudinary. Credenciales por variables de entorno (Cloudinary__CloudName, __ApiKey, __ApiSecret).</summary>
public sealed class CloudinaryImageStorage : IImageStorageService
{
    private readonly Cloudinary _cloudinary;

    public CloudinaryImageStorage(IOptions<CloudinaryOptions> options)
    {
        var o = options.Value;
        _cloudinary = new Cloudinary(new Account(o.CloudName, o.ApiKey, o.ApiSecret)) { Api = { Secure = true } };
    }

    public async Task<StoredImage> UploadAsync(Stream content, string contentType, string folder, CancellationToken ct)
    {
        var result = await _cloudinary.UploadAsync(new ImageUploadParams
        {
            File = new FileDescription("upload", content), Folder = folder, UniqueFilename = true, Overwrite = false
        }, ct);
        if (result.Error is not null) throw new InvalidOperationException($"Cloudinary rechazó la imagen: {result.Error.Message}");
        return new StoredImage(result.SecureUrl.ToString(), result.PublicId);
    }

    public async Task DeleteAsync(string publicId, CancellationToken ct)
    {
        var result = await _cloudinary.DestroyAsync(new DeletionParams(publicId) { ResourceType = ResourceType.Image });
        if (result.Error is not null) throw new InvalidOperationException($"Cloudinary no pudo borrar el archivo: {result.Error.Message}");
        // "ok" o "not found" (ya no existe) son resultados aceptables.
    }
}

/// <summary>Se usa cuando Cloudinary no está configurado: las demás funciones siguen disponibles y la carga de fotos informa el motivo.</summary>
public sealed class UnconfiguredImageStorage : IImageStorageService
{
    public Task<StoredImage> UploadAsync(Stream content, string contentType, string folder, CancellationToken ct) =>
        throw new Domain.Exceptions.ConflictException("El almacenamiento de imágenes no está configurado (variables Cloudinary__*).");

    public Task DeleteAsync(string publicId, CancellationToken ct) =>
        throw new InvalidOperationException("Almacenamiento de imágenes no configurado.");
}

public sealed class BillingOptions
{
    public const string SectionName = "Billing";
    /// <summary>Permite que el propio usuario complete el "pago" de demostración. Desactivar en producción real.</summary>
    public bool DemoPaymentsEnabled { get; set; }
}

/// <summary>Proveedor de pagos de DEMOSTRACIÓN: no cobra nada. Sustituible por una implementación Culqi de IPaymentProvider.</summary>
public sealed class DemoPaymentProvider(IOptions<BillingOptions> options) : IPaymentProvider
{
    public string Name => "DEMO";

    public Task<PaymentResult> ChargeAsync(PaymentRequest request, CancellationToken ct) =>
        Task.FromResult(options.Value.DemoPaymentsEnabled
            ? new PaymentResult(true, IsSimulated: true, Name, "DEMO-" + Guid.NewGuid().ToString("N")[..12], null)
            : new PaymentResult(false, IsSimulated: true, Name, null,
                "Los pagos reales aún no están disponibles. Un administrador puede activar tu plan."));
}
