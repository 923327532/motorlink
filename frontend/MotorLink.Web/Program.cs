using System.Globalization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using MotorLink.Web.Filters;
using MotorLink.Web.Handlers;
using MotorLink.Web.Services.Api;
using MotorLink.Web.Services.Sessions;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

// ---- MVC: antiforgery obligatorio en todo POST/PUT/PATCH/DELETE y manejo central de errores de la API ----
builder.Services.AddControllersWithViews(o =>
{
    o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    o.Filters.Add<ApiExceptionFilter>();
});
builder.Services.AddAntiforgery(o =>
{
    o.Cookie.Name = "MotorLink.Antiforgery";
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.Strict;
    o.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    o.HeaderName = "X-CSRF-TOKEN"; // para las llamadas fetch del asistente, favoritos, etc.
});
builder.Services.AddMemoryCache();
builder.Services.Configure<RouteOptions>(o => o.LowercaseUrls = true);

// ---- Cultura es-PE (formatos de moneda y fecha) ----
var culture = new CultureInfo("es-PE");
builder.Services.Configure<RequestLocalizationOptions>(o =>
{
    o.DefaultRequestCulture = new RequestCulture(culture);
    o.SupportedCultures = [culture];
    o.SupportedUICultures = [culture];
});

// ---- Data Protection persistente (claves en volumen) ----
var keysPath = config["DataProtection:KeysPath"] ?? "data/keys";
if (!Path.IsPathRooted(keysPath)) keysPath = Path.Combine(builder.Environment.ContentRootPath, keysPath);
builder.Services.AddDataProtection().SetApplicationName("MotorLink.Web")
    .PersistKeysToFileSystem(new DirectoryInfo(keysPath));

// ---- Sesiones del lado servidor: los tokens nunca llegan al navegador ----
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.Configure<SessionStoreOptions>(config.GetSection(SessionStoreOptions.SectionName));
builder.Services.AddSingleton<ISessionStore, FileSessionStore>();

// ---- Cookie de sesión: HttpOnly + Secure + SameSite ----
var requireSecureCookie = config.GetValue("Security:SecureCookies", !builder.Environment.IsDevelopment());
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.Cookie.Name = "MotorLink.Session";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Lax;
        o.Cookie.SecurePolicy = requireSecureCookie ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
        o.LoginPath = "/ingresar";
        o.AccessDeniedPath = "/acceso-denegado";
        o.ExpireTimeSpan = TimeSpan.FromDays(7);
        o.SlidingExpiration = false;
        o.Events.OnRedirectToLogin = ctx =>
        {   // Las llamadas AJAX reciben 401 en vez de una redirección HTML
            if (ctx.Request.Headers.XRequestedWith == "XMLHttpRequest") { ctx.Response.StatusCode = 401; return Task.CompletedTask; }
            ctx.Response.Redirect(ctx.RedirectUri);
            return Task.CompletedTask;
        };
        o.Events.OnRedirectToAccessDenied = ctx =>
        {
            if (ctx.Request.Headers.XRequestedWith == "XMLHttpRequest") { ctx.Response.StatusCode = 403; return Task.CompletedTask; }
            ctx.Response.Redirect(ctx.RedirectUri);
            return Task.CompletedTask;
        };
        o.Events.OnValidatePrincipal = async ctx =>
        {
            // Si la sesión del servidor no existe (expirada, revocada o renovación rechazada) la cookie deja de valer.
            var sid = ctx.Principal?.FindFirst(BearerTokenHandler.SessionClaim)?.Value;
            var store = ctx.HttpContext.RequestServices.GetRequiredService<ISessionStore>();
            if (sid is null || await store.GetAsync(sid, ctx.HttpContext.RequestAborted) is null)
            {
                ctx.RejectPrincipal();
                await ctx.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        };
    });
builder.Services.AddAuthorization();

// ---- Clientes HTTP hacia la API (IHttpClientFactory + un cliente especializado por módulo) ----
var apiBase = config["Api:BaseUrl"] ?? throw new InvalidOperationException("Falta Api:BaseUrl.");
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<ITokenProvider, TokenProvider>();
builder.Services.AddTransient<BearerTokenHandler>();
builder.Services.AddHttpClient(TokenProvider.AnonymousClientName, c => c.BaseAddress = new Uri(apiBase));

void AddApi<TService, TImpl>() where TService : class where TImpl : class, TService =>
    builder.Services.AddHttpClient<TService, TImpl>(c => { c.BaseAddress = new Uri(apiBase); c.Timeout = TimeSpan.FromSeconds(30); })
        .AddHttpMessageHandler<BearerTokenHandler>();

AddApi<IAuthApi, AuthApiClient>();
AddApi<IUsersApi, UsersApiClient>();
AddApi<ICatalogApi, CatalogApiClient>();
AddApi<IListingsApi, ListingsApiClient>();
AddApi<IVehiclesApi, VehiclesApiClient>();
AddApi<INegotiationsApi, NegotiationsApiClient>();
AddApi<IBillingApi, BillingApiClient>();
AddApi<IAccountApi, AccountApiClient>();
AddApi<IAdminApi, AdminApiClient>();

builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
    o.ForwardLimit = 1;
});
builder.Services.AddScoped<MotorLink.Web.Services.Sessions.IWebSessionService, MotorLink.Web.Services.Sessions.WebSessionService>();
builder.Services.AddScoped<MotorLink.Web.Services.IFavoriteLookup, MotorLink.Web.Services.FavoriteLookup>();
builder.Services.AddScoped<MotorLink.Web.Areas.Admin.Controllers.UserDirectory>();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseForwardedHeaders();
app.UseRequestLocalization();

if (!app.Environment.IsDevelopment()) app.UseExceptionHandler("/error/500");
if (config.GetValue("Security:RequireHttps", false))
{
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.UseStatusCodePagesWithReExecute("/error/{0}");

// Cabeceras de seguridad (mitigación de XSS, clickjacking y sniffing). Las imágenes pueden venir de Cloudinary.
app.Use(async (ctx, next) =>
{
    var h = ctx.Response.Headers;
    h["X-Content-Type-Options"] = "nosniff";
    h["X-Frame-Options"] = "DENY";
    h["Referrer-Policy"] = "strict-origin-when-cross-origin";
    h["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    h["Content-Security-Policy"] =
        "default-src 'self'; img-src 'self' data: https://res.cloudinary.com; script-src 'self'; style-src 'self'; font-src 'self'; connect-src 'self'; " +
        "frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
    await next();
});

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapHealthChecks("/health");
app.MapAreaControllerRoute(name: "dashboard", areaName: "Dashboard", pattern: "panel/{controller=Home}/{action=Index}/{id?}").WithStaticAssets();
app.MapAreaControllerRoute(name: "admin", areaName: "Admin", pattern: "admin/{controller=Home}/{action=Index}/{id?}").WithStaticAssets();
app.MapControllers().WithStaticAssets();

app.Run();

public partial class Program;
