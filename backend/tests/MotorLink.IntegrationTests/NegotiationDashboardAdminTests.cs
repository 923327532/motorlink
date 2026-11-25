using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MotorLink.IntegrationTests;

[Collection("api")]
public sealed class NegotiationTests(ApiFactory factory)
{
    private readonly TestApi _api = new(factory);

    private async Task<(Session Owner, Session Buyer, Guid ListingId)> SetupAsync(object[]? ops = null)
    {
        var admin = await _api.AdminAsync();
        var owner = await _api.NewUserAsync();
        var buyer = await _api.NewUserAsync();
        return (owner, buyer, await _api.PublishAsync(owner, admin, ops ?? [TestApi.WorkRent(65)]));
    }

    [Fact]
    public async Task Work_rent_example_65_55_60_accept_keeps_full_history_and_notifies_each_party()
    {
        var (owner, buyer, lid) = await SetupAsync();

        var start = await buyer.Json_(buyer.Post("/api/v1/negotiations", new { listingId = lid, operationType = "WORK_RENT", amount = 55, periodicity = "DAILY", message = "¿Podemos en 55?" }), HttpStatusCode.Created);
        var nid = start.GetProperty("negotiation").GetProperty("id").GetGuid();
        Assert.Equal("PENDING", start.GetProperty("negotiation").GetProperty("status").GetString());
        Assert.Equal(65m, start.GetProperty("listedPrice").GetDecimal());
        Assert.Equal("PEN", start.GetProperty("negotiation").GetProperty("currency").GetString());

        // el propietario ve la propuesta y debe responder; el comprador no
        Assert.True((await owner.Json_(owner.Get($"/api/v1/negotiations/{nid}"))).GetProperty("awaitingMyResponse").GetBoolean());
        Assert.False((await buyer.Json_(buyer.Get($"/api/v1/negotiations/{nid}"))).GetProperty("awaitingMyResponse").GetBoolean());
        var inbox = await owner.Json_(owner.Get("/api/v1/notifications?unreadOnly=true"));
        Assert.Contains(inbox.GetProperty("items").EnumerateArray(), n => n.GetProperty("type").GetString() == "NEGOTIATION_OFFER");

        // contrapropuesta 60
        var counter = await owner.Json_(owner.Post($"/api/v1/negotiations/{nid}/offers", new { amount = 60, message = "60 y cerramos" }), HttpStatusCode.Created);
        Assert.Equal("COUNTER_OFFER", counter.GetProperty("negotiation").GetProperty("status").GetString());
        var offers = counter.GetProperty("offers").EnumerateArray().ToList();
        Assert.Equal([55m, 60m], offers.Select(o => o.GetProperty("amount").GetDecimal()).ToList());
        var firstOfferId = offers[0].GetProperty("id").GetGuid();
        var lastOfferId = offers[1].GetProperty("id").GetGuid();

        // reglas: no seguidas, no aceptar la propia, no aceptar una anterior
        await Session.Expect(owner.Post($"/api/v1/negotiations/{nid}/offers", new { amount = 61 }), HttpStatusCode.Conflict);
        await Session.Expect(owner.Patch($"/api/v1/negotiations/{nid}/accept", new { offerId = lastOfferId }), HttpStatusCode.Conflict);
        await Session.Expect(buyer.Patch($"/api/v1/negotiations/{nid}/accept", new { offerId = firstOfferId }), HttpStatusCode.Conflict);

        var accepted = await buyer.Json_(buyer.Patch($"/api/v1/negotiations/{nid}/accept", new { offerId = lastOfferId }));
        Assert.Equal("ACCEPTED", accepted.GetProperty("negotiation").GetProperty("status").GetString());
        Assert.Equal(60m, accepted.GetProperty("agreedAmount").GetDecimal());
        Assert.Equal(2, accepted.GetProperty("offers").GetArrayLength());       // historial intacto

        // finalizada: inmodificable
        await Session.Expect(owner.Post($"/api/v1/negotiations/{nid}/offers", new { amount = 70 }), HttpStatusCode.Conflict);
        await Session.Expect(buyer.Patch($"/api/v1/negotiations/{nid}/cancel"), HttpStatusCode.Conflict);
        await Session.Expect(owner.Patch($"/api/v1/negotiations/{nid}/reject"), HttpStatusCode.Conflict);

        var ownerNotes = await owner.Json_(owner.Get("/api/v1/notifications?pageSize=50"));
        var buyerNotes = await buyer.Json_(buyer.Get("/api/v1/notifications?pageSize=50"));
        Assert.Contains(buyerNotes.GetProperty("items").EnumerateArray(), n => n.GetProperty("type").GetString() == "NEGOTIATION_COUNTER");
        Assert.Contains(ownerNotes.GetProperty("items").EnumerateArray(), n => n.GetProperty("type").GetString() == "NEGOTIATION_ACCEPTED");
    }

    [Fact]
    public async Task Participants_only_and_basic_validation_rules()
    {
        var (owner, buyer, lid) = await SetupAsync([TestApi.Rent(80), TestApi.Sale(25000)]);
        var stranger = await _api.NewUserAsync();

        await Session.Expect(owner.Post("/api/v1/negotiations", new { listingId = lid, operationType = "SALE", amount = 100 }), HttpStatusCode.Conflict);   // consigo mismo
        await Session.Expect(buyer.Post("/api/v1/negotiations", new { listingId = lid, operationType = "SALE", amount = 0 }), HttpStatusCode.BadRequest);
        await Session.Expect(buyer.Post("/api/v1/negotiations", new { listingId = lid, operationType = "SALE", amount = -5 }), HttpStatusCode.BadRequest);
        await Session.Expect(buyer.Post("/api/v1/negotiations", new { listingId = lid, operationType = "SALE", amount = 20000, periodicity = "DAILY" }), HttpStatusCode.BadRequest); // venta sin periodicidad
        await Session.Expect(buyer.Post("/api/v1/negotiations", new { listingId = lid, operationType = "RENT", amount = 60 }), HttpStatusCode.BadRequest);                    // alquiler exige periodicidad
        await Session.Expect(buyer.Post("/api/v1/negotiations", new { listingId = lid, operationType = "RENT", amount = 60, periodicity = "MONTHLY" }), HttpStatusCode.BadRequest); // sin tarifa mensual
        await Session.Expect(buyer.Post("/api/v1/negotiations", new { listingId = lid, operationType = "SALE", amount = 20000, currency = "USD" }), HttpStatusCode.BadRequest);   // moneda distinta
        await Session.Expect(buyer.Post("/api/v1/negotiations", new { listingId = lid, operationType = "WORK_RENT", amount = 50, periodicity = "DAILY" }), HttpStatusCode.BadRequest); // modalidad inexistente
        await Session.Expect(buyer.Post("/api/v1/negotiations", new { listingId = Guid.NewGuid(), operationType = "SALE", amount = 100 }), HttpStatusCode.NotFound);

        var n = await buyer.Json_(buyer.Post("/api/v1/negotiations", new { listingId = lid, operationType = "SALE", amount = 20000 }), HttpStatusCode.Created);
        var nid = n.GetProperty("negotiation").GetProperty("id").GetGuid();
        await Session.Expect(buyer.Post("/api/v1/negotiations", new { listingId = lid, operationType = "SALE", amount = 21000 }), HttpStatusCode.Conflict); // ya hay una abierta

        // Terceros no ven ni modifican (404, no revela existencia)
        await Session.Expect(stranger.Get($"/api/v1/negotiations/{nid}"), HttpStatusCode.NotFound);
        await Session.Expect(stranger.Post($"/api/v1/negotiations/{nid}/offers", new { amount = 1 }), HttpStatusCode.NotFound);
        await Session.Expect(stranger.Patch($"/api/v1/negotiations/{nid}/cancel"), HttpStatusCode.NotFound);
        Assert.Equal(0, (await stranger.Json_(stranger.Get("/api/v1/negotiations"))).GetProperty("totalCount").GetInt32());

        // Moneda y periodicidad se mantienen en las contrapropuestas
        var c = await owner.Json_(owner.Post($"/api/v1/negotiations/{nid}/offers", new { amount = 24000, currency = "USD" }), HttpStatusCode.Created);
        Assert.All(c.GetProperty("offers").EnumerateArray(), o => Assert.Equal("PEN", o.GetProperty("currency").GetString()));

        // Filtros por rol
        Assert.Equal(1, (await buyer.Json_(buyer.Get("/api/v1/negotiations?role=SENT"))).GetProperty("totalCount").GetInt32());
        Assert.Equal(0, (await buyer.Json_(buyer.Get("/api/v1/negotiations?role=RECEIVED"))).GetProperty("totalCount").GetInt32());
        Assert.Equal(1, (await owner.Json_(owner.Get("/api/v1/negotiations?role=RECEIVED&status=COUNTER_OFFER"))).GetProperty("totalCount").GetInt32());

        // Rechazo por la contraparte y cancelación
        await Session.Expect(owner.Patch($"/api/v1/negotiations/{nid}/reject"), HttpStatusCode.Conflict); // su propia contraoferta: se cancela, no se rechaza
        await Session.Expect(buyer.Patch($"/api/v1/negotiations/{nid}/reject"), HttpStatusCode.OK);
        // tras finalizar, se puede abrir otra
        await Session.Expect(buyer.Post("/api/v1/negotiations", new { listingId = lid, operationType = "SALE", amount = 22000 }), HttpStatusCode.Created);
    }

    [Fact]
    public async Task Simultaneous_responses_do_not_both_succeed_and_history_stays_consistent()
    {
        var (owner, buyer, lid) = await SetupAsync([TestApi.Sale(30000)]);
        var n = await buyer.Json_(buyer.Post("/api/v1/negotiations", new { listingId = lid, operationType = "SALE", amount = 25000 }), HttpStatusCode.Created);
        var nid = n.GetProperty("negotiation").GetProperty("id").GetGuid();
        var offerId = n.GetProperty("offers")[0].GetProperty("id").GetGuid();

        // El propietario envía dos acciones contradictorias al mismo tiempo: aceptar y rechazar
        var results = await Task.WhenAll(
            owner.Patch($"/api/v1/negotiations/{nid}/accept", new { offerId }),
            owner.Patch($"/api/v1/negotiations/{nid}/reject"),
            owner.Post($"/api/v1/negotiations/{nid}/offers", new { amount = 28000 }));
        Assert.Equal(1, results.Count(r => r.IsSuccessStatusCode));
        Assert.All(results.Where(r => !r.IsSuccessStatusCode), r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));

        var detail = await owner.Json_(owner.Get($"/api/v1/negotiations/{nid}"));
        var seqs = detail.GetProperty("offers").EnumerateArray().Select(o => o.GetProperty("sequence").GetInt32()).ToList();
        Assert.Equal(seqs.OrderBy(x => x).ToList(), seqs);
        Assert.Equal(seqs.Distinct().Count(), seqs.Count);
    }

    [Fact]
    public async Task Inactive_listings_block_new_proposals_and_open_negotiations_are_cancelled_on_sale_or_expire()
    {
        var (owner, buyer, lid) = await SetupAsync([TestApi.Sale(30000)]);
        var n = await buyer.Json_(buyer.Post("/api/v1/negotiations", new { listingId = lid, operationType = "SALE", amount = 25000 }), HttpStatusCode.Created);
        var nid = n.GetProperty("negotiation").GetProperty("id").GetGuid();

        await Session.Expect(owner.Patch($"/api/v1/listings/{lid}/status", new { status = "PAUSED" }), HttpStatusCode.OK);
        var d = await buyer.Json_(buyer.Get($"/api/v1/negotiations/{nid}"));
        Assert.Equal("CANCELLED", d.GetProperty("negotiation").GetProperty("status").GetString());
        Assert.Contains((await buyer.Json_(buyer.Get("/api/v1/notifications?pageSize=50"))).GetProperty("items").EnumerateArray(),
            x => x.GetProperty("type").GetString() == "NEGOTIATION_CANCELLED");

        var other = await _api.NewUserAsync();
        await Session.Expect(other.Post("/api/v1/negotiations", new { listingId = lid, operationType = "SALE", amount = 100 }), HttpStatusCode.Conflict);

        // Vencimiento: se fuerza la fecha y corre el proceso periódico
        await Session.Expect(owner.Patch($"/api/v1/listings/{lid}/status", new { status = "PUBLISHED" }), HttpStatusCode.OK);
        var n2 = await other.Json_(other.Post("/api/v1/negotiations", new { listingId = lid, operationType = "SALE", amount = 26000 }), HttpStatusCode.Created);
        var nid2 = n2.GetProperty("negotiation").GetProperty("id").GetGuid();
        await _api.WithServicesAsync(async sp =>
        {
            var db = sp.GetRequiredService<MotorLink.Infrastructure.Persistence.AppDbContext>();
            var neg = await db.Negotiations.FirstAsync(x => x.Id == nid2);
            neg.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
            Assert.True(await sp.GetRequiredService<MotorLink.Application.Negotiations.INegotiationService>().ExpireDueAsync(default) >= 1);
        });
        var expired = await other.Json_(other.Get($"/api/v1/negotiations/{nid2}"));
        Assert.Equal("EXPIRED", expired.GetProperty("negotiation").GetProperty("status").GetString());
        await Session.Expect(owner.Post($"/api/v1/negotiations/{nid2}/offers", new { amount = 27000 }), HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Favorites_are_idempotent_listed_and_removable()
    {
        var admin = await _api.AdminAsync();
        var owner = await _api.NewUserAsync();
        var fan = await _api.NewUserAsync();
        var lid = await _api.PublishAsync(owner, admin);

        await Session.Expect(fan.Put($"/api/v1/favorites/{lid}"), HttpStatusCode.Created);
        await Session.Expect(fan.Put($"/api/v1/favorites/{lid}"), HttpStatusCode.NoContent);   // sin duplicados
        await Session.Expect(fan.Put($"/api/v1/favorites/{Guid.NewGuid()}"), HttpStatusCode.NotFound);
        var list = await fan.Json_(fan.Get("/api/v1/favorites"));
        Assert.Equal(1, list.GetProperty("totalCount").GetInt32());
        Assert.Equal(lid, list.GetProperty("items")[0].GetProperty("id").GetGuid());
        Assert.Equal(0, (await owner.Json_(owner.Get("/api/v1/favorites"))).GetProperty("totalCount").GetInt32()); // aislados por usuario

        await Session.Expect(fan.Delete($"/api/v1/favorites/{lid}"), HttpStatusCode.NoContent);
        await Session.Expect(fan.Delete($"/api/v1/favorites/{lid}"), HttpStatusCode.NotFound);
        await Session.Expect(_api.AnonymousSession().Get("/api/v1/favorites"), HttpStatusCode.Unauthorized);
    }
}

[Collection("api")]
public sealed class DashboardAdminTests(ApiFactory factory)
{
    private readonly TestApi _api = new(factory);

    [Fact]
    public async Task Dashboard_returns_real_numbers_with_defined_formulas_and_safe_division()
    {
        var admin = await _api.AdminAsync();
        var owner = await _api.NewUserAsync();
        await _api.GrantPlanAsync(admin, owner, "PROFESSIONAL"); // estadísticas avanzadas

        // Sin actividad: tasas en 0 (sin división entre cero)
        var empty = await owner.Json_(owner.Get("/api/v1/dashboard"));
        Assert.Equal(0, empty.GetProperty("conversionRate").GetDouble());
        Assert.Equal(0, empty.GetProperty("contactRate").GetDouble());

        var l1 = await _api.PublishAsync(owner, admin, [TestApi.Sale(30000), TestApi.Rent(80)], title: "Publicación uno");
        var l2 = await _api.PublishAsync(owner, admin, [TestApi.Sale(20000)], title: "Publicación dos");
        var b1 = await _api.NewUserAsync(); var b2 = await _api.NewUserAsync(); var b3 = await _api.NewUserAsync();

        foreach (var b in new[] { b1, b2, b3 }) await Session.Expect(b.Post($"/api/v1/listings/{l1}/views"), HttpStatusCode.NoContent);
        await Session.Expect(b1.Post($"/api/v1/listings/{l1}/contact", new { channel = "WHATSAPP" }), HttpStatusCode.OK);

        // 3 propuestas recibidas (2 en l1, 1 en l2); se acepta 1; el dueño además envía una propuesta a otro anunciante
        var n1 = await b1.Json_(b1.Post("/api/v1/negotiations", new { listingId = l1, operationType = "SALE", amount = 25000 }), HttpStatusCode.Created);
        await Session.Expect(b2.Post("/api/v1/negotiations", new { listingId = l1, operationType = "SALE", amount = 26000 }), HttpStatusCode.Created);
        await Session.Expect(b3.Post("/api/v1/negotiations", new { listingId = l2, operationType = "SALE", amount = 15000 }), HttpStatusCode.Created);
        await Session.Expect(owner.Patch($"/api/v1/negotiations/{n1.GetProperty("negotiation").GetProperty("id").GetGuid()}/accept", new { offerId = n1.GetProperty("offers")[0].GetProperty("id").GetGuid() }), HttpStatusCode.OK);
        var other = await _api.NewUserAsync();
        var otherListing = await _api.PublishAsync(other, admin, [TestApi.Sale(10000)], title: "Publicación ajena");
        await Session.Expect(owner.Post("/api/v1/negotiations", new { listingId = otherListing, operationType = "SALE", amount = 9000 }), HttpStatusCode.Created);

        // una venta y un alquiler registrados
        await Session.Expect(owner.Patch($"/api/v1/listings/{l2}/status", new { status = "SOLD" }), HttpStatusCode.OK);
        await Session.Expect(owner.Patch($"/api/v1/listings/{l1}/status", new { status = "RENTED", operationType = "RENT" }), HttpStatusCode.OK);

        var d = await owner.Json_(owner.Get($"/api/v1/dashboard?from={DateTime.UtcNow.AddDays(-1):O}&to={DateTime.UtcNow.AddDays(1):O}"));
        Assert.Equal(3, d.GetProperty("views").GetInt32());
        Assert.Equal(1, d.GetProperty("contactClicks").GetInt32());
        Assert.Equal(3, d.GetProperty("proposalsReceived").GetInt32());
        Assert.Equal(1, d.GetProperty("proposalsSent").GetInt32());
        Assert.Equal(1, d.GetProperty("acceptedNegotiations").GetInt32());
        Assert.Equal(1, d.GetProperty("vehiclesSold").GetInt32());
        Assert.Equal(1, d.GetProperty("vehiclesRented").GetInt32());
        Assert.Equal(2, d.GetProperty("vehiclesRegistered").GetInt32());
        Assert.Equal(Math.Round(1 / 3.0, 4), d.GetProperty("conversionRate").GetDouble());     // aceptadas / recibidas
        Assert.Equal(Math.Round(1 / 3.0, 4), d.GetProperty("contactRate").GetDouble());        // clics / visualizaciones
        Assert.Equal("PROFESSIONAL", d.GetProperty("planCode").GetString());
        Assert.Equal(15, d.GetProperty("planUsage").GetProperty("maxActiveListings").GetInt32());

        var monthly = d.GetProperty("monthlyEvolution");
        Assert.True(monthly.GetArrayLength() >= 1);
        Assert.Equal(3, monthly.EnumerateArray().Sum(m => m.GetProperty("views").GetInt32()));

        // Filtro por fechas: un rango pasado no incluye nada
        var past = await owner.Json_(owner.Get($"/api/v1/dashboard?from={DateTime.UtcNow.AddDays(-30):O}&to={DateTime.UtcNow.AddDays(-20):O}"));
        Assert.Equal(0, past.GetProperty("views").GetInt32());
        Assert.Equal(0, past.GetProperty("conversionRate").GetDouble());
        await Session.Expect(owner.Get($"/api/v1/dashboard?from={DateTime.UtcNow:O}&to={DateTime.UtcNow.AddDays(-5):O}"), HttpStatusCode.BadRequest);

        // El propietario ajeno no ve datos de otros
        var stranger = await _api.NewUserAsync();
        Assert.Equal(0, (await stranger.Json_(stranger.Get("/api/v1/dashboard"))).GetProperty("views").GetInt32());
    }

    [Fact]
    public async Task Advanced_stats_and_report_export_depend_on_the_plan()
    {
        var admin = await _api.AdminAsync();
        var free = await _api.NewUserAsync();
        var free1 = await free.Json_(free.Get("/api/v1/dashboard"));
        Assert.False(free1.GetProperty("advancedStats").GetBoolean());
        Assert.Equal(JsonValueKind.Null, free1.GetProperty("monthlyEvolution").ValueKind);
        await Session.Expect(free.Get("/api/v1/dashboard/export"), HttpStatusCode.Forbidden);

        var biz = await _api.NewUserAsync();
        await _api.GrantPlanAsync(admin, biz, "BUSINESS");
        var csv = await biz.Get("/api/v1/dashboard/export");
        Assert.Equal(HttpStatusCode.OK, csv.StatusCode);
        Assert.Contains("indicador,valor", await csv.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Admin_can_block_and_reactivate_accounts_and_every_admin_action_is_audited()
    {
        var admin = await _api.AdminAsync();
        var anon = _api.AnonymousSession();
        var victim = await _api.NewUserAsync();

        await Session.Expect(victim.Get("/api/v1/admin/users"), HttpStatusCode.Forbidden);
        await Session.Expect(admin.Patch($"/api/v1/admin/users/{admin.UserId}/status", new { isActive = false }), HttpStatusCode.Conflict); // no se auto-bloquea

        await Session.Expect(admin.Patch($"/api/v1/admin/users/{victim.UserId}/status", new { isActive = false }), HttpStatusCode.OK);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.Post("/api/v1/auth/login", new { email = victim.Email, password = "Abcdef12" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.Post("/api/v1/auth/refresh", new { refreshToken = victim.RefreshToken })).StatusCode);

        await Session.Expect(admin.Patch($"/api/v1/admin/users/{victim.UserId}/status", new { isActive = true }), HttpStatusCode.OK);
        Assert.Equal(HttpStatusCode.OK, (await anon.Post("/api/v1/auth/login", new { email = victim.Email, password = "Abcdef12" })).StatusCode);

        var users = await admin.Json_(admin.Get($"/api/v1/admin/users?search={victim.Email}"));
        Assert.Equal(1, users.GetProperty("totalCount").GetInt32());

        var audit = await admin.Json_(admin.Get($"/api/v1/admin/audit-logs?action=user.&userId={admin.UserId}"));
        var actions = audit.GetProperty("items").EnumerateArray().Select(a => a.GetProperty("action").GetString()).ToList();
        Assert.Contains("user.block", actions);
        Assert.Contains("user.activate", actions);
        await Session.Expect(victim.Get("/api/v1/admin/audit-logs"), HttpStatusCode.Forbidden);

        var stats = await admin.Json_(admin.Get("/api/v1/admin/stats"));
        Assert.True(stats.GetProperty("usersRegistered").GetInt32() >= 2);
        Assert.Equal(12, stats.GetProperty("monthlyGrowth").GetArrayLength());
        Assert.Equal("text/csv", (await admin.Get("/api/v1/admin/reports/users.csv")).Content.Headers.ContentType?.MediaType);
    }
}

[Collection("api")]
public sealed class AdminDirectoryTests(ApiFactory factory)
{
    private readonly TestApi _api = new(factory);

    [Fact]
    public async Task Admin_directories_are_read_only_protected_and_never_expose_full_plates()
    {
        var admin = await _api.AdminAsync();
        var owner = await _api.NewUserAsync("BUSINESS");
        var buyer = await _api.NewUserAsync();
        var plate = TestApi.RandomPlate();
        var lid = await _api.PublishAsync(owner, admin, [TestApi.Sale(30000)], TestApi.VehicleBody(plate: plate));
        await Session.Expect(buyer.Post("/api/v1/negotiations", new { listingId = lid, operationType = "SALE", amount = 25000 }), HttpStatusCode.Created);

        foreach (var path in new[] { "businesses", "vehicles", "negotiations" })
            await Session.Expect(buyer.Get($"/api/v1/admin/{path}"), HttpStatusCode.Forbidden);

        var biz = await admin.Json_(admin.Get($"/api/v1/admin/businesses?search={owner.Email}"));
        Assert.Equal(1, biz.GetProperty("totalCount").GetInt32());
        Assert.Equal("Autos Ana", biz.GetProperty("items")[0].GetProperty("businessName").GetString());

        var veh = await admin.Json_(admin.Get($"/api/v1/admin/vehicles?search={owner.Email}"));
        Assert.DoesNotContain(plate, veh.GetRawText());
        Assert.Equal(plate[..2] + new string('*', plate.Length - 2), veh.GetProperty("items")[0].GetProperty("maskedPlate").GetString());
        Assert.Equal("PUBLISHED", veh.GetProperty("items")[0].GetProperty("listingStatus").GetString());

        var neg = await admin.Json_(admin.Get("/api/v1/admin/negotiations?status=PENDING"));
        Assert.Contains(neg.GetProperty("items").EnumerateArray(), n => n.GetProperty("buyerEmail").GetString() == buyer.Email);
    }
}
