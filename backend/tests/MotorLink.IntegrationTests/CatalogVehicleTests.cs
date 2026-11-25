using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MotorLink.Application.Vehicles;
using MotorLink.Infrastructure.Persistence;

namespace MotorLink.IntegrationTests;

[Collection("api")]
public sealed class CatalogTests(ApiFactory factory)
{
    private readonly TestApi _api = new(factory);

    [Fact]
    public async Task Public_catalogs_return_initial_types_and_filter_models_by_brand_and_type()
    {
        var anon = _api.AnonymousSession();
        var types = await anon.Json_(anon.Get("/api/v1/catalogs/vehicle-types"));
        var names = types.EnumerateArray().Select(t => t.GetProperty("name").GetString()).ToList();
        foreach (var expected in new[] { "Auto", "Motocicleta", "Camioneta", "Camión", "Furgoneta", "Mototaxi", "Miniván", "Bus" })
            Assert.Contains(expected, names);

        var toyota = await anon.Json_(anon.Get("/api/v1/catalogs/models?brandId=1"));
        Assert.All(toyota.EnumerateArray(), m => Assert.Equal(1, m.GetProperty("brandId").GetInt32()));

        var buses = await anon.Json_(anon.Get("/api/v1/catalogs/models?vehicleTypeId=3")); // BUS
        Assert.All(buses.EnumerateArray(), m => Assert.Contains(3, m.GetProperty("vehicleTypeIds").EnumerateArray().Select(x => x.GetInt32())));

        var provinces = await anon.Json_(anon.Get("/api/v1/catalogs/provinces?departmentId=15"));
        Assert.All(provinces.EnumerateArray(), p => Assert.Equal(15, p.GetProperty("departmentId").GetInt32()));
    }

    [Fact]
    public async Task Admin_crud_protects_records_in_use_and_allows_deactivation()
    {
        var admin = await _api.AdminAsync();
        var user = await _api.NewUserAsync();

        // Un usuario normal no administra catálogos
        await Session.Expect(user.Post("/api/v1/admin/catalogs/brands", new { name = "Marca X" }), HttpStatusCode.Forbidden);

        var brand = await admin.Json_(admin.Post("/api/v1/admin/catalogs/brands", new { name = "Marca" + Guid.NewGuid().ToString("N")[..6] }), HttpStatusCode.Created);
        var brandId = brand.GetProperty("id").GetInt32();
        var model = await admin.Json_(admin.Post("/api/v1/admin/catalogs/models", new { brandId, name = "Modelo Uno", vehicleTypeIds = new[] { 1, 4 } }), HttpStatusCode.Created);
        var modelId = model.GetProperty("id").GetInt32();

        // Duplicados y referencias inválidas
        await Session.Expect(admin.Post("/api/v1/admin/catalogs/models", new { brandId, name = "Modelo Uno", vehicleTypeIds = new[] { 1 } }), HttpStatusCode.Conflict);
        await Session.Expect(admin.Post("/api/v1/admin/catalogs/models", new { brandId = 99999, name = "X", vehicleTypeIds = new[] { 1 } }), HttpStatusCode.BadRequest);
        await Session.Expect(admin.Post("/api/v1/admin/catalogs/models", new { brandId, name = "Y", vehicleTypeIds = new[] { 99999 } }), HttpStatusCode.BadRequest);

        // La marca tiene modelos => no se puede eliminar
        await Session.Expect(admin.Delete($"/api/v1/admin/catalogs/brands/{brandId}"), HttpStatusCode.Conflict);

        // Un vehículo usa el modelo => el modelo no se puede eliminar ni dejar de ser compatible con el tipo usado
        var vid = await _api.CreateVehicleAsync(user, TestApi.VehicleBody(typeId: 1, brandId: brandId, modelId: modelId));
        Assert.NotEqual(Guid.Empty, vid);
        await Session.Expect(admin.Delete($"/api/v1/admin/catalogs/models/{modelId}"), HttpStatusCode.Conflict);
        await Session.Expect(admin.Put($"/api/v1/admin/catalogs/models/{modelId}", new { brandId, name = "Modelo Uno", vehicleTypeIds = new[] { 4 } }), HttpStatusCode.Conflict);

        // Desactivar sí se permite y lo oculta del catálogo público
        await Session.Expect(admin.Patch($"/api/v1/admin/catalogs/brands/{brandId}/active", new { isActive = false }), HttpStatusCode.NoContent);
        var anon = _api.AnonymousSession();
        var brands = await anon.Json_(anon.Get("/api/v1/catalogs/brands"));
        Assert.DoesNotContain(brands.EnumerateArray(), b => b.GetProperty("id").GetInt32() == brandId);
        // y ya no se pueden registrar vehículos con esa marca
        await Session.Expect(user.Post("/api/v1/vehicles", TestApi.VehicleBody(brandId: brandId, modelId: modelId)), HttpStatusCode.BadRequest);

        // Un modelo sin vehículos sí se elimina
        var free = await admin.Json_(admin.Post("/api/v1/admin/catalogs/models", new { brandId, name = "Sin uso", vehicleTypeIds = new[] { 1 } }), HttpStatusCode.Created);
        await Session.Expect(admin.Delete($"/api/v1/admin/catalogs/models/{free.GetProperty("id").GetInt32()}"), HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Geography_hierarchy_is_validated_and_codes_must_be_consistent()
    {
        var admin = await _api.AdminAsync();
        // Provincia con código que no corresponde a su departamento (15 = Lima)
        await Session.Expect(admin.Post("/api/v1/admin/catalogs/provinces", new { departmentId = 15, code = "0999", name = "Falsa" }), HttpStatusCode.BadRequest);
        var prov = await admin.Json_(admin.Post("/api/v1/admin/catalogs/provinces", new { departmentId = 15, code = "1599", name = "Provincia de Prueba" }), HttpStatusCode.Created);
        var provId = prov.GetProperty("id").GetInt32();
        await Session.Expect(admin.Post("/api/v1/admin/catalogs/districts", new { provinceId = provId, code = "150101", name = "Mal" }), HttpStatusCode.BadRequest);
        var dist = await admin.Json_(admin.Post("/api/v1/admin/catalogs/districts", new { provinceId = provId, code = "159901", name = "Distrito de Prueba" }), HttpStatusCode.Created);

        // Departamento y provincia con hijos no se eliminan
        await Session.Expect(admin.Delete($"/api/v1/admin/catalogs/provinces/{provId}"), HttpStatusCode.Conflict);
        await Session.Expect(admin.Delete("/api/v1/admin/catalogs/departments/15"), HttpStatusCode.Conflict);
        // En orden inverso sí
        await Session.Expect(admin.Delete($"/api/v1/admin/catalogs/districts/{dist.GetProperty("id").GetInt32()}"), HttpStatusCode.NoContent);
        await Session.Expect(admin.Delete($"/api/v1/admin/catalogs/provinces/{provId}"), HttpStatusCode.NoContent);
    }
}

[Collection("api")]
public sealed class VehicleTests(ApiFactory factory)
{
    private readonly TestApi _api = new(factory);

    [Fact]
    public async Task Vehicle_crud_normalizes_plate_and_hides_other_users_resources()
    {
        var a = await _api.NewUserAsync();
        var b = await _api.NewUserAsync();
        var plate = TestApi.RandomPlate();

        var created = await a.Json_(a.Post("/api/v1/vehicles", TestApi.VehicleBody(plate: plate.ToLower().Insert(2, "-"))), HttpStatusCode.Created);
        var id = created.GetProperty("id").GetGuid();
        Assert.Equal(plate, created.GetProperty("plate").GetString()); // normalizada
        Assert.False(created.GetProperty("ownershipVerified").GetBoolean());

        // Placa duplicada (misma placa con otro formato) => 409
        await Session.Expect(b.Post("/api/v1/vehicles", TestApi.VehicleBody(plate: plate)), HttpStatusCode.Conflict);

        // Aislamiento entre usuarios
        await Session.Expect(b.Get($"/api/v1/vehicles/{id}"), HttpStatusCode.NotFound);
        await Session.Expect(b.Put($"/api/v1/vehicles/{id}", TestApi.VehicleBody()), HttpStatusCode.NotFound);
        await Session.Expect(b.Delete($"/api/v1/vehicles/{id}"), HttpStatusCode.NotFound);
        var listB = await b.Json_(b.Get("/api/v1/vehicles"));
        Assert.Equal(0, listB.GetProperty("totalCount").GetInt32());

        // Actualizar y eliminar (la placa queda libre)
        var upd = await a.Json_(a.Put($"/api/v1/vehicles/{id}", TestApi.VehicleBody(plate: plate, mileage: 45000)));
        Assert.Equal(45000, upd.GetProperty("mileage").GetInt32());
        await Session.Expect(a.Delete($"/api/v1/vehicles/{id}"), HttpStatusCode.NoContent);
        await Session.Expect(a.Get($"/api/v1/vehicles/{id}"), HttpStatusCode.NotFound);
        await Session.Expect(b.Post("/api/v1/vehicles", TestApi.VehicleBody(plate: plate)), HttpStatusCode.Created);
    }

    [Fact]
    public async Task Technical_fields_are_validated_by_vehicle_type()
    {
        var u = await _api.NewUserAsync();
        // Mototaxi (type 8, modelo 41 Torito RE): sin puertas, cilindrada opcional
        await Session.Expect(u.Post("/api/v1/vehicles", TestApi.VehicleBody(typeId: 8, brandId: 11, modelId: 41, doors: 4, seats: 3, cc: null)), HttpStatusCode.BadRequest);
        await Session.Expect(u.Post("/api/v1/vehicles", TestApi.VehicleBody(typeId: 8, brandId: 11, modelId: 41, doors: null, seats: 3, cc: null)), HttpStatusCode.Created);
        // Bus (type 3, modelo 44 O 500): mínimo 10 asientos
        await Session.Expect(u.Post("/api/v1/vehicles", TestApi.VehicleBody(typeId: 3, brandId: 14, modelId: 44, doors: 2, seats: 5)), HttpStatusCode.BadRequest);
        await Session.Expect(u.Post("/api/v1/vehicles", TestApi.VehicleBody(typeId: 3, brandId: 14, modelId: 44, doors: 2, seats: 40)), HttpStatusCode.Created);
        // Camión (type 9, modelo 42 Hino 300): requiere capacidad de carga
        await Session.Expect(u.Post("/api/v1/vehicles", TestApi.VehicleBody(typeId: 9, brandId: 12, modelId: 42, doors: 2, seats: 2, load: null)), HttpStatusCode.BadRequest);
        await Session.Expect(u.Post("/api/v1/vehicles", TestApi.VehicleBody(typeId: 9, brandId: 12, modelId: 42, doors: 2, seats: 2, load: 5000)), HttpStatusCode.Created);
        // Auto con 7 puertas
        await Session.Expect(u.Post("/api/v1/vehicles", TestApi.VehicleBody(doors: 7)), HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Brand_model_type_compatibility_is_enforced()
    {
        var u = await _api.NewUserAsync();
        // Yaris (1) no es un bus; Hilux (3) es camioneta no auto; marca incorrecta para el modelo
        await Session.Expect(u.Post("/api/v1/vehicles", TestApi.VehicleBody(typeId: 3, modelId: 1)), HttpStatusCode.BadRequest);
        await Session.Expect(u.Post("/api/v1/vehicles", TestApi.VehicleBody(typeId: 1, modelId: 3)), HttpStatusCode.BadRequest);
        await Session.Expect(u.Post("/api/v1/vehicles", TestApi.VehicleBody(brandId: 2, modelId: 1)), HttpStatusCode.BadRequest);
        await Session.Expect(u.Post("/api/v1/vehicles", TestApi.VehicleBody(typeId: 4, modelId: 3, doors: 4, seats: 5)), HttpStatusCode.Created);
    }

    [Fact]
    public async Task Plate_rules_depend_on_condition_and_years_are_checked()
    {
        var u = await _api.NewUserAsync();
        var noPlate = TestApi.VehicleBody(condition: "USED"); // se reemplaza la placa por null
        var json = System.Text.RegularExpressions.Regex.Replace(JsonSerializer.Serialize(noPlate, Json.Options), "\"plate\":\"[^\"]+\"", "\"plate\":null");
        var r = await u.Client.PostAsync("/api/v1/vehicles", new StringContent(json, System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode); // usado sin placa

        var newNoPlate = JsonSerializer.Serialize(TestApi.VehicleBody(condition: "NEW", mileage: 10, year: DateTime.UtcNow.Year), Json.Options);
        newNoPlate = System.Text.RegularExpressions.Regex.Replace(newNoPlate, "\"plate\":\"[^\"]+\"", "\"plate\":null");
        var ok = await u.Client.PostAsync("/api/v1/vehicles", new StringContent(newNoPlate, System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Created, ok.StatusCode); // nuevo sin placa permitido

        await Session.Expect(u.Post("/api/v1/vehicles", TestApi.VehicleBody(condition: "NEW", mileage: 5000, year: DateTime.UtcNow.Year)), HttpStatusCode.BadRequest); // nuevo con 5000 km
        await Session.Expect(u.Post("/api/v1/vehicles", TestApi.VehicleBody(year: 1900)), HttpStatusCode.BadRequest);
    }
}

[Collection("api")]
public sealed class ImageTests(ApiFactory factory)
{
    private readonly TestApi _api = new(factory);

    private static Task<HttpResponseMessage> Upload(Session s, Guid vid, byte[] bytes, string contentType) =>
        s.Client.PostAsync($"/api/v1/vehicles/{vid}/images", TestApi.FileContent(bytes, contentType));

    [Fact]
    public async Task Upload_validates_real_content_size_and_format()
    {
        var u = await _api.NewUserAsync();
        var vid = await _api.CreateVehicleAsync(u);

        Assert.Equal(HttpStatusCode.Created, (await Upload(u, vid, TestApi.PngBytes(), "image/png")).StatusCode);
        // Ejecutable renombrado como imagen: se rechaza por contenido
        var exe = new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00, 0xFF, 0xFF };
        Assert.Equal(HttpStatusCode.BadRequest, (await Upload(u, vid, exe, "image/png")).StatusCode);
        // Tipo declarado distinto al real (PNG declarado como JPEG)
        Assert.Equal(HttpStatusCode.BadRequest, (await Upload(u, vid, TestApi.PngBytes(), "image/jpeg")).StatusCode);
        // GIF no permitido
        Assert.Equal(HttpStatusCode.BadRequest, (await Upload(u, vid, "GIF89a......"u8.ToArray(), "image/gif")).StatusCode);
        // > 5 MB
        var big = TestApi.PngBytes(5 * 1024 * 1024 + 10);
        Assert.Equal(HttpStatusCode.BadRequest, (await Upload(u, vid, big, "image/png")).StatusCode);
        // JPEG y WebP válidos
        Assert.Equal(HttpStatusCode.Created, (await Upload(u, vid, [0xFF, 0xD8, 0xFF, 0xE0, 0, 0, 0, 0, 0, 0, 0, 0], "image/jpeg")).StatusCode);
        var webp = "RIFF\0\0\0\0WEBPVP8 "u8.ToArray();
        Assert.Equal(HttpStatusCode.Created, (await Upload(u, vid, webp, "image/webp")).StatusCode);
    }

    [Fact]
    public async Task Photo_limit_comes_from_the_plan_and_other_users_cannot_touch_the_vehicle()
    {
        var u = await _api.NewUserAsync();
        var other = await _api.NewUserAsync();
        var vid = await _api.CreateVehicleAsync(u);

        for (var i = 0; i < 5; i++) Assert.Equal(HttpStatusCode.Created, (await Upload(u, vid, TestApi.PngBytes(), "image/png")).StatusCode);
        var sixth = await Upload(u, vid, TestApi.PngBytes(), "image/png");
        Assert.Equal(HttpStatusCode.Forbidden, sixth.StatusCode); // GRATIS = 5 fotos
        using var problem = JsonDocument.Parse(await sixth.Content.ReadAsStringAsync());
        Assert.Equal("MAX_PHOTOS_PER_VEHICLE", problem.RootElement.GetProperty("code").GetString());
        Assert.Equal(5, factory.Storage.Stored.Count(kv => kv.Key.Contains(vid.ToString("N")) || kv.Key.Contains(vid.ToString())));

        Assert.Equal(HttpStatusCode.NotFound, (await Upload(other, vid, TestApi.PngBytes(), "image/png")).StatusCode);

        // Con el plan BASIC (10 fotos) vuelve a permitirse
        var admin = await _api.AdminAsync();
        await _api.GrantPlanAsync(admin, u, "BASIC");
        Assert.Equal(HttpStatusCode.Created, (await Upload(u, vid, TestApi.PngBytes(), "image/png")).StatusCode);
    }

    [Fact]
    public async Task Photos_can_be_reordered_cover_selected_and_deleted()
    {
        var u = await _api.NewUserAsync();
        var vid = await _api.CreateVehicleAsync(u);
        var ids = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            var img = await u.Json_(Task.FromResult(await Upload(u, vid, TestApi.PngBytes(), "image/png")), HttpStatusCode.Created);
            ids.Add(img.GetProperty("id").GetGuid());
        }
        var list = await u.Json_(u.Get($"/api/v1/vehicles/{vid}/images"));
        Assert.True(list[0].GetProperty("isCover").GetBoolean()); // la primera es portada

        var reversed = ids.AsEnumerable().Reverse().ToList();
        var ordered = await u.Json_(u.Put($"/api/v1/vehicles/{vid}/images/order", new { imageIds = reversed }));
        Assert.Equal(reversed[0], ordered[0].GetProperty("id").GetGuid());
        await Session.Expect(u.Put($"/api/v1/vehicles/{vid}/images/order", new { imageIds = new[] { ids[0] } }), HttpStatusCode.BadRequest);

        var cover = await u.Json_(u.Patch($"/api/v1/vehicles/{vid}/images/{ids[1]}/cover"));
        Assert.Single(cover.EnumerateArray().Where(i => i.GetProperty("isCover").GetBoolean()));

        // Al borrar la portada, otra la reemplaza
        await Session.Expect(u.Delete($"/api/v1/vehicles/{vid}/images/{ids[1]}"), HttpStatusCode.NoContent);
        var after = await u.Json_(u.Get($"/api/v1/vehicles/{vid}/images"));
        Assert.Equal(2, after.GetArrayLength());
        Assert.Single(after.EnumerateArray().Where(i => i.GetProperty("isCover").GetBoolean()));
    }

    [Fact]
    public async Task Provider_failures_leave_a_trace_and_the_cleanup_job_removes_orphans()
    {
        var u = await _api.NewUserAsync();
        var vid = await _api.CreateVehicleAsync(u);
        var img = await u.Json_(Task.FromResult(await Upload(u, vid, TestApi.PngBytes(), "image/png")), HttpStatusCode.Created);

        // El proveedor falla al borrar: la fila se elimina de SQL Server y el archivo queda registrado como huérfano.
        factory.Storage.FailDeletes = true;
        await Session.Expect(u.Delete($"/api/v1/vehicles/{vid}/images/{img.GetProperty("id").GetGuid()}"), HttpStatusCode.NoContent);
        factory.Storage.FailDeletes = false;

        var storedBefore = factory.Storage.Stored.Count;
        await _api.WithServicesAsync(async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            Assert.True(await db.OrphanedImages.AnyAsync());
            var cleaned = await sp.GetRequiredService<IImageMaintenance>().CleanOrphansAsync(default);
            Assert.True(cleaned >= 1);
            Assert.False(await db.OrphanedImages.AnyAsync());
        });
        Assert.True(factory.Storage.Stored.Count < storedBefore);

        // Si el proveedor falla al SUBIR no queda nada en BD
        factory.Storage.FailUploads = true;
        Assert.True((int)(await Upload(u, vid, TestApi.PngBytes(), "image/png")).StatusCode >= 500);
        factory.Storage.FailUploads = false;
        var list = await u.Json_(u.Get($"/api/v1/vehicles/{vid}/images"));
        Assert.Equal(0, list.GetArrayLength());
    }
}
