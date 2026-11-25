namespace MotorLink.Web.ViewModels;

/// <summary>Elemento de navegación lateral. <see cref="Controller"/>/<see cref="Action"/> determinan el estado activo.</summary>
public sealed record NavItem(string Label, string Url, string Icon, string Controller, string? Action = null, string? Group = null, string? Badge = null);

public sealed record SideNavModel(IReadOnlyList<NavItem> Items, string LogoutLabel = "Cerrar sesión");

public static class NavMenus
{
    public static readonly IReadOnlyList<NavItem> Dashboard =
    [
        new("Resumen", "/panel", "layout-dashboard", "Home", Group: "Panel"),
        new("Mis vehículos", "/panel/vehicles", "car", "Vehicles", Group: "Publicar"),
        new("Mis publicaciones", "/panel/listings", "megaphone", "Listings"),
        new("Propuestas recibidas", "/panel/negotiations/received", "inbox", "Negotiations", "Received", Group: "Negociación"),
        new("Propuestas enviadas", "/panel/negotiations/sent", "send", "Negotiations", "Sent"),
        new("Favoritos", "/panel/favorites", "heart", "Favorites"),
        new("Mi suscripción", "/panel/subscription", "badge-check", "Subscription", "Index", Group: "Cuenta"),
        new("Historial de pagos", "/panel/subscription/payments", "receipt", "Subscription", "Payments"),
        new("Mi perfil", "/panel/profile", "user", "Profile"),
        new("Seguridad", "/panel/security", "lock-keyhole", "Security"),
    ];

    public static readonly IReadOnlyList<NavItem> Admin =
    [
        new("Dashboard", "/admin", "layout-dashboard", "Home", Group: "General"),
        new("Usuarios", "/admin/users", "users", "Users", Group: "Personas"),
        new("Empresas", "/admin/businesses", "building-2", "Businesses"),
        new("Vehículos", "/admin/vehicles", "car", "Vehicles", Group: "Marketplace"),
        new("Publicaciones", "/admin/listings", "megaphone", "Listings"),
        new("Negociaciones", "/admin/negotiations", "handshake", "Negotiations"),
        new("Planes", "/admin/plans", "layers", "Plans", Group: "Suscripciones"),
        new("Suscripciones", "/admin/subscriptions", "badge-check", "Subscriptions"),
        new("Pagos", "/admin/payments", "receipt", "Payments"),
        new("Marcas y modelos", "/admin/catalog", "tags", "Catalog", Group: "Catálogos"),
        new("Ubicaciones", "/admin/locations", "map-pin", "Locations"),
        new("Reportes", "/admin/reports", "chart-column", "Reports", Group: "Análisis"),
        new("Auditoría", "/admin/audit", "scroll-text", "Audit"),
    ];
}

public sealed record EmptyStateModel(string Icon, string Title, string Text, string? ActionText = null, string? ActionUrl = null);

public sealed record CatalogRowActions(string Kind, int Id, bool IsActive, string Tab, string Name);
