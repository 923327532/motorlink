using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MotorLink.Web.Services;
using MotorLink.Web.Services.Api;
using MotorLink.Web.ViewModels;

namespace MotorLink.Web.Controllers;

[AllowAnonymous]
public sealed class HomeController(IListingsApi listings, ICatalogApi catalogs, IBillingApi billing, IFavoriteLookup favorites) : Controller
{
    [HttpGet("/")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        // Todas las secciones salen de la API; las consultas independientes se ejecutan en paralelo.
        var recentTask = listings.SearchAsync("pageSize=24", ct);          // orden por defecto: destacadas primero, luego las más recientes
        var workTask = listings.SearchAsync("operation=WORK_RENT&pageSize=4", ct);
        var plansTask = billing.PlansAsync(ct);
        var typesTask = catalogs.VehicleTypesAsync(ct);
        var depsTask = catalogs.DepartmentsAsync(ct);
        var favTask = favorites.IdsAsync(ct);
        await Task.WhenAll(recentTask, workTask, plansTask, typesTask, depsTask, favTask);

        var recent = recentTask.Result.Success ? recentTask.Result.Value!.Items : [];
        var types = typesTask.Result;

        // Categorías con su cantidad real de publicaciones
        (string Label, string Icon, string Code)[] defs =
            [("Autos", "car-front", "CAR"), ("Motocicletas", "bike", "MOTORCYCLE"), ("Camionetas", "truck", "PICKUP"), ("Camiones", "container", "TRUCK"), ("Vehículos comerciales", "package", "VAN")];
        var counts = await Task.WhenAll(defs.Select(async d =>
        {
            var id = types.FirstOrDefault(t => t.Code == d.Code)?.Id;
            if (id is null) return new CategoryTile(d.Label, d.Icon, null, 0);
            var r = await listings.SearchAsync($"vehicleTypeId={id}&pageSize=1", ct);
            return new CategoryTile(d.Label, d.Icon, id, r.Success ? r.Value!.TotalCount : 0);
        }));

        var featured = recent.Where(l => l.IsFeatured).Take(4).ToList();
        var latest = recent.Where(l => !l.IsFeatured).OrderByDescending(l => l.PublishedAt).Take(8).ToList();

        return View(new HomeViewModel(featured, workTask.Result.Success ? workTask.Result.Value!.Items : [], latest,
            plansTask.Result.Success ? plansTask.Result.Value! : [], types, depsTask.Result, counts, favTask.Result));
    }

    [HttpGet("terminos")]
    public IActionResult Terms() => View();

    [HttpGet("privacidad")]
    public IActionResult Privacy() => View();

    [AcceptVerbs("GET", "POST", "PUT", "PATCH", "DELETE"), Route("error/{code:int}")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error(int code)
    {
        Response.StatusCode = code is >= 400 and < 600 ? code : 500;
        return View(code);
    }
}
