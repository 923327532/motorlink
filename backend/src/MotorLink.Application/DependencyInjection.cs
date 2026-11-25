using Microsoft.Extensions.DependencyInjection;
using MotorLink.Application.Admin;
using MotorLink.Application.Auth;
using MotorLink.Application.Catalogs;
using MotorLink.Application.Common;
using MotorLink.Application.Dashboard;
using MotorLink.Application.Listings;
using MotorLink.Application.Negotiations;
using MotorLink.Application.Notifications;
using MotorLink.Application.Subscriptions;
using MotorLink.Application.Users;
using MotorLink.Application.Vehicles;

namespace MotorLink.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();

        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<INotificationService, NotificationService>();

        services.AddScoped<ICatalogQueries, CatalogQueries>();
        services.AddScoped<ICatalogAdminService, CatalogAdminService>();

        services.AddScoped<IVehicleService, VehicleService>();
        services.AddScoped<IVehicleImageService, VehicleImageService>();
        services.AddScoped<IImageMaintenance, ImageMaintenance>();

        services.AddScoped<ISubscriptionPermissions, SubscriptionPermissions>();
        services.AddScoped<IPlanLimitEnforcer, PlanLimitEnforcer>();
        services.AddScoped<IPlanService, PlanService>();
        services.AddScoped<ISubscriptionService, SubscriptionService>();
        services.AddScoped<ISubscriptionMaintenance, SubscriptionMaintenance>();

        services.AddScoped<IListingLifecycle, ListingLifecycle>();
        services.AddScoped<IListingService, ListingService>();
        services.AddScoped<IListingModerationService, ListingModerationService>();
        services.AddScoped<IPublicListingService, PublicListingService>();
        services.AddScoped<IFavoriteService, FavoriteService>();

        services.AddScoped<INegotiationService, NegotiationService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IAdminService, AdminService>();
        services.AddScoped<IAdminDirectoryService, AdminDirectoryService>();
        return services;
    }
}
