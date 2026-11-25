using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MotorLink.Domain.Entities;

namespace MotorLink.Infrastructure.Persistence;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";
    /// <summary>Aplica migraciones al iniciar la API (útil en desarrollo/Docker).</summary>
    public bool MigrateOnStartup { get; set; }
    /// <summary>Si ambos están definidos (variables de entorno), crea el administrador inicial.</summary>
    public string? AdminEmail { get; set; }
    public string? AdminPassword { get; set; }
}

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, DatabaseOptions options, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseInitializer");
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (options.MigrateOnStartup)
        {
            // SQL Server en contenedor puede tardar en aceptar conexiones: reintentos.
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    await db.Database.MigrateAsync(ct);
                    logger.LogInformation("Migraciones aplicadas.");
                    break;
                }
                catch (Exception ex) when (attempt < 10)
                {
                    logger.LogWarning("Base de datos no disponible (intento {Attempt}/10): {Message}", attempt, ex.Message);
                    await Task.Delay(TimeSpan.FromSeconds(5), ct);
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(options.AdminEmail) && !string.IsNullOrWhiteSpace(options.AdminPassword))
            await EnsureAdminAsync(scope.ServiceProvider, options, logger);
    }

    private static async Task EnsureAdminAsync(IServiceProvider sp, DatabaseOptions options, ILogger logger)
    {
        var users = sp.GetRequiredService<UserManager<ApplicationUser>>();
        if (await users.FindByEmailAsync(options.AdminEmail!) is not null) return;

        var admin = new ApplicationUser
        {
            Id = Guid.NewGuid(), UserName = options.AdminEmail, Email = options.AdminEmail, EmailConfirmed = true,
            Profile = new UserProfile { FirstName = "Administrador", LastName = "MotorLink" }
        };
        admin.Profile.UserId = admin.Id;

        var result = await users.CreateAsync(admin, options.AdminPassword!);
        if (!result.Succeeded)
        {
            logger.LogError("No se pudo crear el administrador inicial: {Errors}", string.Join("; ", result.Errors.Select(e => e.Code)));
            return;
        }
        await users.AddToRolesAsync(admin, [RoleNames.Admin, RoleNames.User]);
        logger.LogInformation("Administrador inicial creado.");
    }
}
