using System.Net;
using System.Text.Json;
using MotorLink.Domain.Common;

namespace MotorLink.IntegrationTests;

[Collection("api")]
public sealed class ListingTests(ApiFactory factory)
{
    private readonly TestApi _api = new(factory);

    [Fact]
    public async Task Operations_are_validated_per_modality_and_work_rent_requires_conditions()
    {
        var u = await _api.NewUserAsync();
        var vid = await _api.CreateVehicleAsync(u);

        // WORK_RENT sin condiciones, venta sin precio, alquiler con precio de venta, modalidad repetida
        await Session.Expect(u.Post("/api/v1/listings", TestApi.Listing(vid, [new { operationType = "WORK_RENT", dailyRate = 60 }])), HttpStatusCode.BadRequest);
        await Session.Expect(u.Post("/api/v1/listings", TestApi.Listing(vid, [new { operationType = "SALE" }])), HttpStatusCode.BadRequest);
        await Session.Expect(u.Post("/api/v1/listings", TestApi.Listing(vid, [new { operationType = "RENT", salePrice = 100, dailyRate = 50 }])), HttpStatusCode.BadRequest);
        await Session.Expect(u.Post("/api/v1/listings", TestApi.Listing(vid, [TestApi.Sale(1000), TestApi.Sale(2000)])), HttpStatusCode.BadRequest);
        // Ubicación incoherente (distrito de Lima con provincia de Callao)
        await Session.Expect(u.Post("/api/v1/listings", TestApi.Listing(vid, null, districtId: 150122, provinceId: 701)), HttpStatusCode.BadRequest);

        // Una publicación con las tres modalidades comparte las características técnicas del vehículo
        var l = await u.Json_(u.Post("/api/v1/listings", TestApi.Listing(vid, [TestApi.Sale(30000), TestApi.Rent(80), TestApi.WorkRent(65)])), HttpStatusCode.Created);
        Assert.Equal(3, l.GetProperty("operations").GetArrayLength());
        var work = l.GetProperty("operations").EnumerateArray().First(o => o.GetProperty("operationType").GetString() == "WORK_RENT");
        Assert.Equal("TAXI", work.GetProperty("workRent").GetProperty("allowedUse").GetString());
        Assert.Equal("DRIVER", work.GetProperty("workRent").GetProperty("fuelPaidBy").GetString());

        // El mismo vehículo no puede tener dos publicaciones
        await Session.Expect(u.Post("/api/v1/listings", TestApi.Listing(vid)), HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Moderation_flow_state_machine_and_visibility()
    {
        var owner = await _api.NewUserAsync();
        var other = await _api.NewUserAsync();
        var admin = await _api.AdminAsync();
        var anon = _api.AnonymousSession();

        // Sin fotografías no se puede enviar a revisión
        var (_, noPhoto) = await _api.CreateDraftAsync(owner, photo: false);
        await Session.Expect(owner.Patch($"/api/v1/listings/{noPhoto}/status", new { status = "PENDING_REVIEW" }), HttpStatusCode.BadRequest);

        var (vid, lid) = await _api.CreateDraftAsync(owner);
        // Transición inválida y recursos ajenos
        await Session.Expect(owner.Patch($"/api/v1/listings/{lid}/status", new { status = "PUBLISHED" }), HttpStatusCode.Conflict);
        await Session.Expect(other.Patch($"/api/v1/listings/{lid}/status", new { status = "PENDING_REVIEW" }), HttpStatusCode.NotFound);
        await Session.Expect(other.Get($"/api/v1/listings/mine/{lid}"), HttpStatusCode.NotFound);
        // Borrador no es público
        await Session.Expect(anon.Get($"/api/v1/listings/{lid}"), HttpStatusCode.NotFound);

        await _api.SubmitAsync(owner, lid);
        // Solo ADMIN modera
        await Session.Expect(owner.Post($"/api/v1/admin/listings/{lid}/approve"), HttpStatusCode.Forbidden);
        var queue = await admin.Json_(admin.Get("/api/v1/admin/listings"));
        Assert.Contains(queue.GetProperty("items").EnumerateArray(), i => i.GetProperty("id").GetGuid() == lid);

        // Rechazo exige motivo, queda registrado y notifica al propietario
        await Session.Expect(admin.Post($"/api/v1/admin/listings/{lid}/reject", new { reason = "x" }), HttpStatusCode.BadRequest);
        await Session.Expect(admin.Post($"/api/v1/admin/listings/{lid}/reject", new { reason = "Fotos poco claras del vehículo" }), HttpStatusCode.NoContent);
        var rejected = await owner.Json_(owner.Get($"/api/v1/listings/mine/{lid}"));
        Assert.Equal("REJECTED", rejected.GetProperty("status").GetString());
        Assert.Contains("Fotos", rejected.GetProperty("rejectionReason").GetString());
        var notes = await owner.Json_(owner.Get("/api/v1/notifications?unreadOnly=true"));
        Assert.Contains(notes.GetProperty("items").EnumerateArray(), n => n.GetProperty("type").GetString() == "LISTING_REJECTED");

        // Editar, reenviar y aprobar
        await Session.Expect(owner.Patch($"/api/v1/listings/{lid}/status", new { status = "DRAFT" }), HttpStatusCode.OK);
        await Session.Expect(owner.Put($"/api/v1/listings/{lid}", TestApi.Listing(vid, [TestApi.Sale(24000)], "Yaris corregido")), HttpStatusCode.OK);
        await _api.SubmitAsync(owner, lid);
        await Session.Expect(admin.Post($"/api/v1/admin/listings/{lid}/approve"), HttpStatusCode.NoContent);
        // Una publicada no se edita directamente
        await Session.Expect(owner.Put($"/api/v1/listings/{lid}", TestApi.Listing(vid)), HttpStatusCode.Conflict);

        // Detalle público: sin placa completa, sin teléfono, sin titularidad
        var detail = await anon.Json_(anon.Get($"/api/v1/listings/{lid}"));
        var raw = detail.GetRawText();
        var mine = await owner.Json_(owner.Get($"/api/v1/vehicles/{vid}"));
        var plate = mine.GetProperty("plate").GetString()!;
        Assert.DoesNotContain(plate, raw);
        Assert.DoesNotContain("987654321", raw);
        Assert.DoesNotContain("legalOwner", raw, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(plate[..2] + new string('*', plate.Length - 2), detail.GetProperty("vehicle").GetProperty("maskedPlate").GetString());
        Assert.True(detail.GetProperty("contactAvailable").GetBoolean());
        Assert.True(detail.GetProperty("isAvailable").GetBoolean());

        // Pausar oculta de la búsqueda; reactivar la muestra
        await Session.Expect(owner.Patch($"/api/v1/listings/{lid}/status", new { status = "PAUSED" }), HttpStatusCode.OK);
        Assert.False(await SearchContains(anon, "", lid));
        await Session.Expect(owner.Patch($"/api/v1/listings/{lid}/status", new { status = "PUBLISHED" }), HttpStatusCode.OK);
        Assert.True(await SearchContains(anon, "", lid));
    }

    [Fact]
    public async Task Reserved_rented_and_sold_states_control_availability_and_close_modalities()
    {
        var owner = await _api.NewUserAsync();
        await _api.GrantPlanAsync(await _api.AdminAsync(), owner, "PROFESSIONAL");
        var admin = await _api.AdminAsync();
        var anon = _api.AnonymousSession();

        var lid = await _api.PublishAsync(owner, admin, [TestApi.Sale(30000), TestApi.Rent(80)]);

        // RESERVED/RENTED exigen la modalidad
        await Session.Expect(owner.Patch($"/api/v1/listings/{lid}/status", new { status = "RESERVED" }), HttpStatusCode.BadRequest);
        // Una venta no puede marcarse como alquilada
        await Session.Expect(owner.Patch($"/api/v1/listings/{lid}/status", new { status = "RENTED", operationType = "SALE" }), HttpStatusCode.BadRequest);

        // Alquilar la modalidad RENT => deja de figurar como disponible (también la venta)
        var rented = await owner.Json_(owner.Patch($"/api/v1/listings/{lid}/status", new { status = "RENTED", operationType = "RENT" }));
        Assert.Equal("RENTED", rented.GetProperty("status").GetString());
        Assert.False(await SearchContains(anon, "", lid));                         // búsqueda por defecto: solo disponibles
        Assert.True(await SearchContains(anon, "availability=RENTED", lid));
        var detail = await anon.Json_(anon.Get($"/api/v1/listings/{lid}"));
        Assert.False(detail.GetProperty("isAvailable").GetBoolean());
        // No se puede negociar ni contactar una publicación no disponible
        var buyer = await _api.NewUserAsync();
        await Session.Expect(buyer.Post("/api/v1/negotiations", new { listingId = lid, operationType = "SALE", amount = 20000 }), HttpStatusCode.Conflict);
        await Session.Expect(buyer.Post($"/api/v1/listings/{lid}/contact", new { channel = "WHATSAPP" }), HttpStatusCode.NotFound);

        // Devuelto => vuelve a estar disponible
        await Session.Expect(owner.Patch($"/api/v1/listings/{lid}/status", new { status = "PUBLISHED" }), HttpStatusCode.OK);
        Assert.True(await SearchContains(anon, "", lid));

        // Vendido => se cierran todas las modalidades activas
        var sold = await owner.Json_(owner.Patch($"/api/v1/listings/{lid}/status", new { status = "SOLD" }));
        var ops = sold.GetProperty("operations").EnumerateArray().ToDictionary(o => o.GetProperty("operationType").GetString()!, o => o.GetProperty("status").GetString());
        Assert.Equal("SOLD", ops["SALE"]);
        Assert.Equal("CLOSED", ops["RENT"]);
        Assert.False(await SearchContains(anon, "availability=ALL", lid));
        await Session.Expect(owner.Patch($"/api/v1/listings/{lid}/status", new { status = "PUBLISHED" }), HttpStatusCode.Conflict); // vendido es terminal
    }

    [Fact]
    public async Task Search_filters_sort_and_paginate_in_sql_and_never_mix_currencies()
    {
        var admin = await _api.AdminAsync();
        var owner = await _api.NewUserAsync();
        await _api.GrantPlanAsync(admin, owner, "BUSINESS"); // 40 publicaciones
        var anon = _api.AnonymousSession();
        var tag = "Zq" + Guid.NewGuid().ToString("N")[..6];   // texto único para aislar esta prueba

        // Toyota Yaris 2018 PEN 20000 (Lima) · Hyundai Accent 2022 PEN 35000 (Lima) · Toyota Hilux 2021 USD 30000 (Arequipa) · alquiler 70 PEN
        var a = await _api.PublishAsync(owner, admin, [TestApi.Sale(20000)], TestApi.VehicleBody(year: 2018, mileage: 90000), $"{tag} Yaris barato");
        var b = await _api.PublishAsync(owner, admin, [TestApi.Sale(35000, negotiable: false)], TestApi.VehicleBody(brandId: 2, modelId: 6, year: 2022, mileage: 10000, fuel: "DIESEL"), $"{tag} Accent nuevo");
        var vHilux = await _api.CreateVehicleAsync(owner, TestApi.VehicleBody(typeId: 4, modelId: 3, year: 2021));
        await _api.UploadPhotoAsync(owner, vHilux);
        var hilux = (await owner.Json_(owner.Post("/api/v1/listings", TestApi.Listing(vHilux, [TestApi.Sale(30000, "USD")], $"{tag} Hilux", districtId: 40101, provinceId: 401, departmentId: 4)), HttpStatusCode.Created)).GetProperty("id").GetGuid();
        await _api.SubmitAsync(owner, hilux);
        await Session.Expect(admin.Post($"/api/v1/admin/listings/{hilux}/approve"), HttpStatusCode.NoContent);
        var c = await _api.PublishAsync(owner, admin, [TestApi.Rent(70)], TestApi.VehicleBody(), $"{tag} Yaris alquiler");

        async Task<List<Guid>> Ids(string qs) =>
            (await anon.Json_(anon.Get($"/api/v1/listings?q={tag}&{qs}"))).GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ToList();

        Assert.Equal(4, (await Ids("")).Count);
        Assert.Equal([a, b, c], (await Ids("pageSize=50")).Where(id => id != hilux).OrderBy(id => new[] { a, b, c }.ToList().IndexOf(id)).ToList());

        // Texto, marca, tipo, año, kilometraje, combustible, ubicación, negociable, operación
        Assert.Equal([a], (await Ids("operation=SALE&brandId=1&yearMax=2019")));
        Assert.Contains(b, await Ids("brandId=2"));
        Assert.DoesNotContain(a, await Ids("fuel=DIESEL"));
        Assert.Equal([b], await Ids("fuel=DIESEL"));
        Assert.DoesNotContain(a, await Ids("maxMileage=50000"));
        Assert.Equal([hilux], await Ids("departmentId=4"));
        Assert.Equal([hilux], await Ids("vehicleTypeId=4"));
        Assert.DoesNotContain(b, await Ids("operation=SALE&negotiable=true"));
        Assert.Equal([c], await Ids("operation=RENT"));
        Assert.Equal(3, (await Ids("operation=SALE")).Count);

        // Precio SIEMPRE dentro de la moneda seleccionada; sin moneda => 400
        await Session.Expect(anon.Get($"/api/v1/listings?priceMax=25000"), HttpStatusCode.BadRequest);
        await Session.Expect(anon.Get($"/api/v1/listings?sort=PRICE_ASC"), HttpStatusCode.BadRequest);
        Assert.Equal([a], await Ids("operation=SALE&currency=PEN&priceMax=25000"));
        Assert.DoesNotContain(hilux, await Ids("operation=SALE&currency=PEN&priceMin=1")); // 30000 USD no entra en PEN
        Assert.Equal([hilux], await Ids("operation=SALE&currency=USD&priceMin=1"));

        // Orden por precio y año
        Assert.Equal([a, b], (await Ids("operation=SALE&currency=PEN&sort=PRICE_ASC")));
        Assert.Equal([b, a], (await Ids("operation=SALE&currency=PEN&sort=PRICE_DESC")));
        Assert.Equal(b, (await Ids("sort=YEAR_DESC")).First());

        // Paginación desde SQL
        var page1 = await anon.Json_(anon.Get($"/api/v1/listings?q={tag}&pageSize=2&page=1"));
        var page2 = await anon.Json_(anon.Get($"/api/v1/listings?q={tag}&pageSize=2&page=2"));
        Assert.Equal(4, page1.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, page1.GetProperty("totalPages").GetInt32());
        Assert.Equal(2, page1.GetProperty("items").GetArrayLength());
        Assert.Equal(2, page2.GetProperty("items").GetArrayLength());
        Assert.Empty(page1.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid())
            .Intersect(page2.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid())));
        await Session.Expect(anon.Get("/api/v1/listings?operation=INVALID"), HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Work_rent_listings_are_searchable_by_permitted_use_and_expose_declared_conditions_only()
    {
        var admin = await _api.AdminAsync();
        var owner = await _api.NewUserAsync();
        var anon = _api.AnonymousSession();
        var lid = await _api.PublishAsync(owner, admin, [TestApi.WorkRent(65)], title: "Taxi listo para trabajar");

        var ids = (await anon.Json_(anon.Get("/api/v1/listings?operation=WORK_RENT&workUse=TAXI&currency=PEN&priceMin=60&priceMax=70")))
            .GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ToList();
        Assert.Contains(lid, ids);
        Assert.DoesNotContain(lid, (await anon.Json_(anon.Get("/api/v1/listings?operation=WORK_RENT&workUse=DELIVERY"))).GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()));

        var detail = await anon.Json_(anon.Get($"/api/v1/listings/{lid}"));
        var wr = detail.GetProperty("operations")[0].GetProperty("workRent");
        Assert.Equal("FULL", wr.GetProperty("shift").GetString());
        Assert.Equal(2, wr.GetProperty("minExperienceYears").GetInt32());
        Assert.Equal("A-IIb", wr.GetProperty("licenseType").GetString());
        Assert.Equal(6000, wr.GetProperty("monthlyKmLimit").GetInt32());
        Assert.Equal("OWNER", wr.GetProperty("insurancePaidBy").GetString());
        Assert.Equal(800m, detail.GetProperty("operations")[0].GetProperty("guarantee").GetDecimal());
    }

    [Fact]
    public async Task Views_contacts_and_whatsapp_links_are_registered_without_confusing_them_with_concreted_deals()
    {
        var admin = await _api.AdminAsync();
        var owner = await _api.NewUserAsync();
        var buyer = await _api.NewUserAsync();
        var anon = _api.AnonymousSession();
        var lid = await _api.PublishAsync(owner, admin);

        // visitas: anónimas cuentan; las del propio anunciante no; el mismo usuario en 30 min cuenta una sola vez
        await Session.Expect(anon.Post($"/api/v1/listings/{lid}/views"), HttpStatusCode.NoContent);
        await Session.Expect(buyer.Post($"/api/v1/listings/{lid}/views"), HttpStatusCode.NoContent);
        await Session.Expect(buyer.Post($"/api/v1/listings/{lid}/views"), HttpStatusCode.NoContent);
        await Session.Expect(owner.Post($"/api/v1/listings/{lid}/views"), HttpStatusCode.NoContent);

        var wa = await buyer.Json_(buyer.Post($"/api/v1/listings/{lid}/contact", new { channel = "WHATSAPP" }));
        Assert.StartsWith("https://wa.me/51987654321?text=", wa.GetProperty("whatsAppUrl").GetString());
        Assert.Contains("no confirma", wa.GetProperty("disclaimer").GetString());
        var ph = await anon.Json_(anon.Post($"/api/v1/listings/{lid}/contact", new { channel = "PHONE" }));
        Assert.Equal("987654321", ph.GetProperty("phoneNumber").GetString());
        await Session.Expect(owner.Post($"/api/v1/listings/{lid}/contact", new { channel = "PHONE" }), HttpStatusCode.Conflict);
        await Session.Expect(anon.Post($"/api/v1/listings/{lid}/contact", new { channel = "EMAIL" }), HttpStatusCode.Conflict);

        var dash = await owner.Json_(owner.Get("/api/v1/dashboard"));
        Assert.Equal(2, dash.GetProperty("views").GetInt32());
        Assert.Equal(2, dash.GetProperty("contactClicks").GetInt32());
        Assert.Equal(0, dash.GetProperty("acceptedNegotiations").GetInt32()); // clics ≠ acuerdos

        var adv = await anon.Json_(anon.Get($"/api/v1/listings/{lid}/advertiser"));
        Assert.Equal("Ana P.", adv.GetProperty("displayName").GetString());
        Assert.DoesNotContain("@", adv.GetRawText());
    }

    private static async Task<bool> SearchContains(Session s, string qs, Guid id)
    {
        var res = await s.Json_(s.Get($"/api/v1/listings?pageSize=50&{qs}"));
        // Se recorre hasta 5 páginas por si hay otras publicaciones de pruebas paralelas
        for (var page = 1; page <= 5; page++)
        {
            var p = await s.Json_(s.Get($"/api/v1/listings?pageSize=50&page={page}&{qs}"));
            if (p.GetProperty("items").EnumerateArray().Any(i => i.GetProperty("id").GetGuid() == id)) return true;
            if (page >= p.GetProperty("totalPages").GetInt32()) break;
        }
        return false;
    }
}
