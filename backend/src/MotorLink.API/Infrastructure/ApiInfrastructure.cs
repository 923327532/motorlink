using Microsoft.AspNetCore.Mvc.ModelBinding;
using MotorLink.Application.Common;
using MotorLink.Application.Listings;
using MotorLink.Application.Negotiations;
using MotorLink.Application.Subscriptions;
using MotorLink.Application.Vehicles;

namespace MotorLink.API.Infrastructure;

public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid? UserId => accessor.HttpContext?.User.GetUserIdOrNullInternal();
    public string? IpAddress => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
}

internal static class PrincipalExtensions
{
    public static Guid? GetUserIdOrNullInternal(this System.Security.Claims.ClaimsPrincipal user)
    {
        var sub = user.FindFirst("sub")?.Value ?? user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(sub, out var id) ? id : null;
    }
}

/// <summary>
/// Permite usar los mismos valores en MAYÚSCULAS que el JSON (PENDING_REVIEW, WORK_RENT) en parámetros de ruta y query string.
/// Se ignoran mayúsculas/minúsculas y guiones bajos.
/// </summary>
public sealed class SnakeCaseEnumBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        var type = Nullable.GetUnderlyingType(context.Metadata.ModelType) ?? context.Metadata.ModelType;
        return type.IsEnum ? new SnakeCaseEnumBinder(type) : null;
    }

    private sealed class SnakeCaseEnumBinder(Type enumType) : IModelBinder
    {
        public Task BindModelAsync(ModelBindingContext bindingContext)
        {
            var value = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
            if (value == ValueProviderResult.None || string.IsNullOrWhiteSpace(value.FirstValue)) return Task.CompletedTask;

            var raw = value.FirstValue!.Replace("_", "");
            foreach (var name in Enum.GetNames(enumType))
                if (string.Equals(name, raw, StringComparison.OrdinalIgnoreCase))
                {
                    bindingContext.Result = ModelBindingResult.Success(Enum.Parse(enumType, name));
                    return Task.CompletedTask;
                }

            if (int.TryParse(raw, out var n) && Enum.IsDefined(enumType, n))
            {
                bindingContext.Result = ModelBindingResult.Success(Enum.ToObject(enumType, n));
                return Task.CompletedTask;
            }

            bindingContext.ModelState.TryAddModelError(bindingContext.ModelName,
                $"Valor inválido. Opciones: {string.Join(", ", Enum.GetNames(enumType).Select(ToSnakeUpper))}.");
            return Task.CompletedTask;
        }

        private static string ToSnakeUpper(string s) =>
            string.Concat(s.Select((c, i) => i > 0 && char.IsUpper(c) ? "_" + c : c.ToString())).ToUpperInvariant();
    }
}

/// <summary>Procesos periódicos: vencimiento de suscripciones, de negociaciones y limpieza de archivos huérfanos.</summary>
public sealed class MaintenanceWorker(IServiceScopeFactory scopes, IConfiguration config, ILogger<MaintenanceWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var minutes = config.GetValue("Maintenance:IntervalMinutes", 10);
        if (minutes <= 0) { logger.LogInformation("Mantenimiento periódico deshabilitado."); return; }

        try { await Task.Delay(TimeSpan.FromSeconds(config.GetValue("Maintenance:StartupDelaySeconds", 30)), stoppingToken); }
        catch (OperationCanceledException) { return; }

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(minutes));
        do { await RunOnceAsync(stoppingToken); }
        while (await WaitAsync(timer, stoppingToken));
    }

    private static async Task<bool> WaitAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try { return await timer.WaitForNextTickAsync(ct); } catch (OperationCanceledException) { return false; }
    }

    public async Task RunOnceAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        await Safe("suscripciones", async () => logger.LogInformation("Suscripciones vencidas: {Count}", await sp.GetRequiredService<ISubscriptionMaintenance>().ExpireDueAsync(ct)));
        await Safe("negociaciones", async () => logger.LogInformation("Negociaciones vencidas: {Count}", await sp.GetRequiredService<INegotiationService>().ExpireDueAsync(ct)));
        await Safe("imágenes", async () => logger.LogInformation("Archivos huérfanos eliminados: {Count}", await sp.GetRequiredService<IImageMaintenance>().CleanOrphansAsync(ct)));
    }

    private async Task Safe(string name, Func<Task> action)
    {
        try { await action(); }
        catch (Exception ex) when (ex is not OperationCanceledException) { logger.LogError(ex, "Falló el mantenimiento de {Task}", name); }
    }
}
