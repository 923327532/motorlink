using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MotorLink.Application.Abstractions;
using MotorLink.Application.Options;
using MotorLink.Domain.Entities;
using MotorLink.Infrastructure.Identity;
using MotorLink.Infrastructure.Persistence;

namespace MotorLink.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                "Falta ConnectionStrings:Default. Defínela con la variable de entorno ConnectionStrings__Default (ver .env.example).");

        services.AddSingleton(TimeProvider.System);

        services.AddDbContext<AppDbContext>(o => o
            .UseSqlServer(connectionString, sql =>
            {
                sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                // Azure SQL puede cortar conexiones de forma transitoria (failover, throttling): reintenta con espera exponencial.
                sql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null);
                sql.CommandTimeout(60);
            })
            // El filtro de borrado lógico es intencional; las relaciones requeridas hacia entidades filtradas
            // se consultan siempre desde el lado dependiente.
            .ConfigureWarnings(w => w.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning)));

        services.AddOptions<JwtOptions>()
            .Bind(config.GetSection(JwtOptions.SectionName))
            .Validate(o => o.Key.Length >= 32, "Jwt:Key debe tener al menos 32 caracteres (variable de entorno Jwt__Key).")
            .Validate(o => o.AccessTokenMinutes > 0 && o.RefreshTokenDays > 0, "Duraciones JWT inválidas.")
            .ValidateOnStart();

        services.AddIdentityCore<ApplicationUser>(o =>
            {
                o.User.RequireUniqueEmail = true;
                o.Password.RequiredLength = 8;
                o.Password.RequireDigit = true;
                o.Password.RequireLowercase = true;
                o.Password.RequireUppercase = true;
                o.Password.RequireNonAlphanumeric = false;
                o.Lockout.AllowedForNewUsers = true;
                o.Lockout.MaxFailedAccessAttempts = 5;
                o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<AppDbContext>();

        services.AddScoped<IIdentityService, IdentityService>();
        services.AddSingleton<ITokenService, TokenService>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IUserProfileRepository, UserProfileRepository>();
        services.AddScoped<IReportQueries, ReportQueries>();
        services.AddScoped<IPlanRevenueReport, PlanRevenueReportService>();
        services.AddScoped<MotorLink.Application.Common.IApplicationDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        // Cloudinary (si no hay credenciales, la carga de fotos informa que no está configurado)
        services.Configure<MotorLink.Infrastructure.Services.CloudinaryOptions>(config.GetSection(MotorLink.Infrastructure.Services.CloudinaryOptions.SectionName));
        if (config.GetSection(MotorLink.Infrastructure.Services.CloudinaryOptions.SectionName).Get<MotorLink.Infrastructure.Services.CloudinaryOptions>()?.IsConfigured == true)
            services.AddSingleton<MotorLink.Application.Vehicles.IImageStorageService, MotorLink.Infrastructure.Services.CloudinaryImageStorage>();
        else if (config.GetValue<bool>("ImageStorage:LocalFallback"))
        {
            services.Configure<MotorLink.Infrastructure.Services.LocalImageOptions>(config.GetSection(MotorLink.Infrastructure.Services.LocalImageOptions.SectionName));
            services.AddSingleton<MotorLink.Infrastructure.Services.LocalDiskImageStorage>();
            services.AddSingleton<MotorLink.Application.Vehicles.IImageStorageService>(sp => sp.GetRequiredService<MotorLink.Infrastructure.Services.LocalDiskImageStorage>());
        }
        else
            services.AddSingleton<MotorLink.Application.Vehicles.IImageStorageService, MotorLink.Infrastructure.Services.UnconfiguredImageStorage>();

        services.Configure<MotorLink.Infrastructure.Services.BillingOptions>(config.GetSection(MotorLink.Infrastructure.Services.BillingOptions.SectionName));
        services.AddSingleton<MotorLink.Application.Subscriptions.IPaymentProvider, MotorLink.Infrastructure.Services.DemoPaymentProvider>();

        services.AddOptions<DatabaseOptions>().Bind(config.GetSection(DatabaseOptions.SectionName));
        return services;
    }
}

/// <summary>Permite ejecutar 'dotnet ef' sin levantar la API (no se conecta a la BD para generar migraciones).</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var cs = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
                 ?? "Server=localhost;Database=MotorLinkDB;Trusted_Connection=True;TrustServerCertificate=True";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(cs, o => o.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)).Options;
        return new AppDbContext(options, TimeProvider.System);
    }
}

