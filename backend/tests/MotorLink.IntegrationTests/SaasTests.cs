using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MotorLink.Application.Subscriptions;
using MotorLink.Domain.Common;
using MotorLink.Infrastructure.Persistence;

namespace MotorLink.IntegrationTests;

[Collection("api")]
public sealed class PlanTests(ApiFactory factory)
{
    private readonly TestApi _api = new(factory);

    [Fact]
    public async Task Five_monthly_plans_exist_with_the_exact_prices_limits_and_benefits()
    {
        var anon = _api.AnonymousSession();
        var plans = (await anon.Json_(anon.Get("/api/v1/plans"))).EnumerateArray().ToDictionary(p => p.GetProperty("code").GetString()!);
        Assert.Equal(["FREE", "BASIC", "PROFESSIONAL", "BUSINESS", "ENTERPRISE"], plans.Keys.OrderBy(k => plans[k].GetProperty("sortOrder").GetInt32()).ToList());

        void Check(string code, decimal price, int listings, int photos, int featured, bool adv, bool biz, bool export, bool support)
        {
            var p = plans[code];
            Assert.Equal(price, p.GetProperty("monthlyPrice").GetDecimal());
            Assert.Equal(listings, p.GetProperty("maxActiveListings").GetInt32());
            Assert.Equal(photos, p.GetProperty("maxPhotosPerVehicle").GetInt32());
            Assert.Equal(featured, p.GetProperty("maxFeaturedListings").GetInt32());
            Assert.Equal(adv, p.GetProperty("hasAdvancedStats").GetBoolean());
            Assert.Equal(biz, p.GetProperty("hasBusinessProfile").GetBoolean());
            Assert.Equal(export, p.GetProperty("canExportReports").GetBoolean());
            Assert.Equal(support, p.GetProperty("hasPrioritySupport").GetBoolean());
        }
        Check("FREE", 0, 2, 5, 0, false, false, false, false);
        Check("BASIC", 19, 5, 10, 1, false, false, false, false);
        Check("PROFESSIONAL", 39, 15, 15, 3, true, true, false, false);
        Check("BUSINESS", 79, 40, 20, 8, true, true, true, false);
        Check("ENTERPRISE", 149, 100, 25, 15, true, true, true, true);
    }

    [Fact]
    public async Task Admin_can_modify_plans_and_the_change_takes_effect_without_code_changes()
    {
        var admin = await _api.AdminAsync();
        var u = await _api.NewUserAsync();
        var plansJson = await admin.Json_(admin.Get("/api/v1/admin/plans"));
        var basic = plansJson.EnumerateArray().First(p => p.GetProperty("code").GetString() == "BASIC");
        var id = basic.GetProperty("id").GetInt32();
        await _api.GrantPlanAsync(admin, u, "BASIC");

        object Body(int listings) => new { code = "BASIC", name = "Básico", monthlyPrice = 19, maxActiveListings = listings, maxPhotosPerVehicle = 10, maxFeaturedListings = 1, hasAdvancedStats = false, hasBusinessProfile = false, canExportReports = false, hasPrioritySupport = false, sortOrder = 2 };
        try
        {
            await Session.Expect(admin.Put($"/api/v1/admin/plans/{id}", Body(1)), HttpStatusCode.OK);
            var cur = await u.Json_(u.Get("/api/v1/subscriptions/current"));
            Assert.Equal(1, cur.GetProperty("usage").GetProperty("maxActiveListings").GetInt32());
        }
        finally { await Session.Expect(admin.Put($"/api/v1/admin/plans/{id}", Body(5)), HttpStatusCode.OK); }

        // Plan gratuito protegido; plan con suscripciones no se elimina; código duplicado => 409
        var free = plansJson.EnumerateArray().First(p => p.GetProperty("code").GetString() == "FREE").GetProperty("id").GetInt32();
        await Session.Expect(admin.Delete($"/api/v1/admin/plans/{free}"), HttpStatusCode.Conflict);
        await Session.Expect(admin.Patch($"/api/v1/admin/plans/{free}/active", new { isActive = false }), HttpStatusCode.Conflict);
        await Session.Expect(admin.Delete($"/api/v1/admin/plans/{id}"), HttpStatusCode.Conflict);
        await Session.Expect(admin.Post("/api/v1/admin/plans", Body(3)), HttpStatusCode.Conflict);
        await Session.Expect(u.Get("/api/v1/admin/plans"), HttpStatusCode.Forbidden);
    }
}

[Collection("api")]
public sealed class SubscriptionTests(ApiFactory factory)
{
    private readonly TestApi _api = new(factory);

    [Fact]
    public async Task New_users_get_the_free_plan_automatically_with_usage_information()
    {
        var u = await _api.NewUserAsync();
        var cur = await u.Json_(u.Get("/api/v1/subscriptions/current"));
        Assert.Equal("FREE", cur.GetProperty("plan").GetProperty("code").GetString());
        Assert.Equal("ACTIVE", cur.GetProperty("subscription").GetProperty("status").GetString());
        var usage = cur.GetProperty("usage");
        Assert.Equal(0, usage.GetProperty("activeListings").GetInt32());
        Assert.Equal(2, usage.GetProperty("maxActiveListings").GetInt32());
        Assert.Equal(5, usage.GetProperty("maxPhotosPerVehicle").GetInt32());
    }

    [Fact]
    public async Task Upgrade_with_demo_payment_is_marked_simulated_and_downgrade_keeps_the_plan_until_it_ends()
    {
        var admin = await _api.AdminAsync();
        var u = await _api.NewUserAsync();

        await Session.Expect(u.Post("/api/v1/subscriptions/change-plan", new { planCode = "NOPE" }), HttpStatusCode.NotFound);
        await Session.Expect(u.Post("/api/v1/subscriptions/change-plan", new { planCode = "FREE" }), HttpStatusCode.Conflict);

        var pending = await u.Json_(u.Post("/api/v1/subscriptions/change-plan", new { planCode = "PROFESSIONAL" }), HttpStatusCode.Created);
        Assert.Equal("PENDING", pending.GetProperty("status").GetString());
        // Hasta pagar, el beneficio NO se aplica
        Assert.Equal("FREE", (await u.Json_(u.Get("/api/v1/subscriptions/current"))).GetProperty("plan").GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, (await u.Get("/api/v1/dashboard/export")).StatusCode);

        var subId = pending.GetProperty("id").GetGuid();
        var other = await _api.NewUserAsync();
        await Session.Expect(other.Post($"/api/v1/subscriptions/{subId}/pay"), HttpStatusCode.NotFound); // ajena
        var active = await u.Json_(u.Post($"/api/v1/subscriptions/{subId}/pay"));
        Assert.Equal("ACTIVE", active.GetProperty("status").GetString());
        Assert.Equal("SIMULATED", active.GetProperty("paymentMode").GetString());
        Assert.NotNull(active.GetProperty("endsAt").GetString());

        var cur = await u.Json_(u.Get("/api/v1/subscriptions/current"));
        Assert.Equal("PROFESSIONAL", cur.GetProperty("plan").GetProperty("code").GetString());
        Assert.Equal(15, cur.GetProperty("usage").GetProperty("maxActiveListings").GetInt32());

        // Pagos: identificados como simulados
        var pays = await u.Json_(u.Get("/api/v1/subscriptions/payments"));
        Assert.All(pays.GetProperty("items").EnumerateArray(), p => Assert.True(p.GetProperty("isSimulated").GetBoolean()));
        Assert.Contains(pays.GetProperty("items").EnumerateArray(), p => p.GetProperty("status").GetString() == "PAID");

        // Renovación extiende el vencimiento desde el final del período vigente
        var endsBefore = active.GetProperty("endsAt").GetDateTime();
        var renewal = await u.Json_(u.Post("/api/v1/subscriptions/renew"), HttpStatusCode.Created);
        var renewed = await u.Json_(u.Post($"/api/v1/subscriptions/{renewal.GetProperty("id").GetGuid()}/pay"));
        Assert.True(renewed.GetProperty("endsAt").GetDateTime() > endsBefore.AddDays(27));

        // Cancelar detiene la renovación pero conserva el plan hasta el vencimiento
        var cancelled = await u.Json_(u.Patch($"/api/v1/subscriptions/{renewed.GetProperty("id").GetGuid()}/cancel"));
        Assert.Equal("ACTIVE", cancelled.GetProperty("status").GetString());
        Assert.NotNull(cancelled.GetProperty("cancelledAt").GetString());
        Assert.Equal("PROFESSIONAL", (await u.Json_(u.Get("/api/v1/subscriptions/current"))).GetProperty("plan").GetProperty("code").GetString());

        // Historial conserva todo
        var hist = await u.Json_(u.Get("/api/v1/subscriptions"));
        Assert.True(hist.GetProperty("totalCount").GetInt32() >= 3); // gratis (reemplazada), profesional (reemplazada por la renovación) y la renovada
    }

    [Fact]
    public async Task Admin_assignment_is_complimentary_and_stats_separate_real_simulated_and_free_mrr()
    {
        var admin = await _api.AdminAsync();
        var before = await admin.Json_(admin.Get("/api/v1/admin/stats"));
        var sim0 = before.GetProperty("mrrSimulated").GetDecimal();
        var comp0 = before.GetProperty("complimentaryValue").GetDecimal();

        var paid = await _api.NewUserAsync();       // BASIC por pago de demostración
        var pend = await paid.Json_(paid.Post("/api/v1/subscriptions/change-plan", new { planCode = "BASIC" }), HttpStatusCode.Created);
        await Session.Expect(admin.Post("/api/v1/admin/payments/demo", new { subscriptionId = pend.GetProperty("id").GetGuid() }), HttpStatusCode.Created);

        var gift = await _api.NewUserAsync();       // ENTERPRISE otorgado
        await _api.GrantPlanAsync(admin, gift, "ENTERPRISE");

        var after = await admin.Json_(admin.Get("/api/v1/admin/stats"));
        Assert.Equal(sim0 + 19m, after.GetProperty("mrrSimulated").GetDecimal());
        Assert.Equal(comp0 + 149m, after.GetProperty("complimentaryValue").GetDecimal());
        Assert.Equal(0m, after.GetProperty("mrrReal").GetDecimal());                 // sin pasarela real no hay ingresos reales
        Assert.True(after.GetProperty("payingSubscribers").GetInt32() >= 2);
        Assert.True(after.GetProperty("planDistribution").GetArrayLength() >= 3);

        // Pagos simulados claramente identificados en administración
        var pays = await admin.Json_(admin.Get("/api/v1/admin/payments?simulated=true"));
        Assert.True(pays.GetProperty("totalCount").GetInt32() >= 1);
        Assert.All(pays.GetProperty("items").EnumerateArray(), p => Assert.True(p.GetProperty("isSimulated").GetBoolean()));

        // ADO.NET desconectado: el reporte DataSet coincide con las estadísticas EF
        var rep = await admin.Json_(admin.Get("/api/v1/admin/reports/plan-revenue"));
        Assert.Equal(after.GetProperty("mrrSimulated").GetDecimal(), rep.GetProperty("totalSimulated").GetDecimal());
        Assert.Equal(after.GetProperty("complimentaryValue").GetDecimal(), rep.GetProperty("totalComplimentary").GetDecimal());
        Assert.Equal(5, rep.GetProperty("plans").GetArrayLength());

        var csv = await admin.Get("/api/v1/admin/reports/subscriptions.csv");
        Assert.Equal("text/csv", csv.Content.Headers.ContentType?.MediaType);
        await Session.Expect(paid.Get("/api/v1/admin/stats"), HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Active_listing_limit_is_enforced_atomically_under_concurrent_requests()
    {
        var u = await _api.NewUserAsync(); // GRATIS: 2 publicaciones activas
        var drafts = new List<Guid>();
        for (var i = 0; i < 5; i++) drafts.Add((await _api.CreateDraftAsync(u, title: $"Auto concurrente {i}")).ListingId);

        // 5 envíos simultáneos: exactamente 2 pueden prosperar
        var results = await Task.WhenAll(drafts.Select(id => u.Patch($"/api/v1/listings/{id}/status", new { status = "PENDING_REVIEW" })));
        Assert.Equal(2, results.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(3, results.Count(r => r.StatusCode == HttpStatusCode.Forbidden));
        var blocked = results.First(r => r.StatusCode == HttpStatusCode.Forbidden);
        Assert.Contains("MAX_ACTIVE_LISTINGS", await blocked.Content.ReadAsStringAsync());

        var cur = await u.Json_(u.Get("/api/v1/subscriptions/current"));
        Assert.Equal(2, cur.GetProperty("usage").GetProperty("activeListings").GetInt32());

        // Subir de plan (activación administrativa) permite más; liberar cupo también
        await _api.GrantPlanAsync(await _api.AdminAsync(), u, "BASIC");
        var rest = drafts.Where((_, i) => results[i].StatusCode != HttpStatusCode.OK).ToList();
        foreach (var id in rest) await Session.Expect(u.Patch($"/api/v1/listings/{id}/status", new { status = "PENDING_REVIEW" }), HttpStatusCode.OK);
        // BASIC = 5: ya está al tope
        var sixth = (await _api.CreateDraftAsync(u, title: "Sexto auto del plan")).ListingId;
        await Session.Expect(u.Patch($"/api/v1/listings/{sixth}/status", new { status = "PENDING_REVIEW" }), HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Featured_and_business_features_are_granted_only_by_plan()
    {
        var admin = await _api.AdminAsync();
        var u = await _api.NewUserAsync();
        var lid = await _api.PublishAsync(u, admin);

        await Session.Expect(u.Patch($"/api/v1/listings/{lid}/featured", new { isFeatured = true }), HttpStatusCode.Forbidden); // GRATIS: 0
        // Anunciante BUSINESS requiere perfil empresarial (PROFESSIONAL+)
        var v = await _api.CreateVehicleAsync(u);
        await Session.Expect(u.Post("/api/v1/listings", TestApi.Listing(v, null, advertiser: "BUSINESS")), HttpStatusCode.Forbidden);

        await _api.GrantPlanAsync(admin, u, "BASIC"); // 1 destacado
        await Session.Expect(u.Patch($"/api/v1/listings/{lid}/featured", new { isFeatured = true }), HttpStatusCode.OK);
        var second = await _api.PublishAsync(u, admin, title: "Segundo auto publicado");
        await Session.Expect(u.Patch($"/api/v1/listings/{second}/featured", new { isFeatured = true }), HttpStatusCode.Forbidden);

        await _api.GrantPlanAsync(admin, u, "PROFESSIONAL");
        await Session.Expect(u.Post("/api/v1/listings", TestApi.Listing(v, null, advertiser: "BUSINESS")), HttpStatusCode.Created);
    }

    [Fact]
    public async Task Expired_plans_fall_back_to_free_limits_pausing_excess_listings_but_keeping_vehicles_and_photos()
    {
        var admin = await _api.AdminAsync();
        var u = await _api.NewUserAsync();
        await _api.GrantPlanAsync(admin, u, "BASIC");

        var listings = new List<Guid>();
        for (var i = 0; i < 4; i++) listings.Add(await _api.PublishAsync(u, admin, title: $"Publicado {i}"));
        await Session.Expect(u.Patch($"/api/v1/listings/{listings[0]}/featured", new { isFeatured = true }), HttpStatusCode.OK);
        var vehiclesBefore = (await u.Json_(u.Get("/api/v1/vehicles"))).GetProperty("totalCount").GetInt32();

        // Se fuerza el vencimiento y se ejecuta el proceso periódico
        await _api.WithServicesAsync(async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            var sub = await db.Subscriptions.FirstAsync(s => s.UserId == u.UserId && s.Status == SubscriptionStatus.Active && s.Plan.Code == "BASIC");
            sub.EndsAt = DateTime.UtcNow.AddMinutes(-5);
            await db.SaveChangesAsync();
            Assert.True(await sp.GetRequiredService<ISubscriptionMaintenance>().ExpireDueAsync(default) >= 1);
        });

        var cur = await u.Json_(u.Get("/api/v1/subscriptions/current"));
        Assert.Equal("FREE", cur.GetProperty("plan").GetProperty("code").GetString());
        Assert.Equal(2, cur.GetProperty("usage").GetProperty("activeListings").GetInt32());

        var mine = await u.Json_(u.Get("/api/v1/listings/mine?pageSize=50"));
        var statuses = mine.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("status").GetString()).ToList();
        Assert.Equal(2, statuses.Count(s => s == "PUBLISHED"));
        Assert.Equal(2, statuses.Count(s => s == "PAUSED"));
        Assert.All(mine.GetProperty("items").EnumerateArray(), i => Assert.False(i.GetProperty("isFeatured").GetBoolean())); // GRATIS: 0 destacadas

        // Nada se elimina
        Assert.Equal(vehiclesBefore, (await u.Json_(u.Get("/api/v1/vehicles"))).GetProperty("totalCount").GetInt32());
        Assert.All(mine.GetProperty("items").EnumerateArray(), i => Assert.True(i.GetProperty("photoCount").GetInt32() >= 1));
        // No puede reactivar las pausadas por encima del límite gratuito
        var paused = mine.GetProperty("items").EnumerateArray().First(i => i.GetProperty("status").GetString() == "PAUSED").GetProperty("id").GetGuid();
        await Session.Expect(u.Patch($"/api/v1/listings/{paused}/status", new { status = "PUBLISHED" }), HttpStatusCode.Forbidden);

        // Se notificó al usuario y se conservó el historial de suscripciones
        var notes = await u.Json_(u.Get("/api/v1/notifications?pageSize=50"));
        Assert.Contains(notes.GetProperty("items").EnumerateArray(), n => n.GetProperty("type").GetString() == "SUBSCRIPTION_EXPIRED");
        Assert.Contains(notes.GetProperty("items").EnumerateArray(), n => n.GetProperty("type").GetString() == "LISTINGS_PAUSED");
        var hist = await u.Json_(u.Get("/api/v1/subscriptions?pageSize=50"));
        Assert.Contains(hist.GetProperty("items").EnumerateArray(), s => s.GetProperty("status").GetString() == "EXPIRED");
    }
}
