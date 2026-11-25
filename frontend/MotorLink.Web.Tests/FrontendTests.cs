using System.ComponentModel.DataAnnotations;
using System.Net;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MotorLink.Web.Extensions;
using MotorLink.Web.Services.Api;
using MotorLink.Web.Services.Sessions;
using MotorLink.Web.ViewModels;

namespace MotorLink.Web.Tests;

public class UiFormattingTests
{
    private static OperationSummary Op(string type, decimal? sale = null, decimal? daily = null, decimal? weekly = null, decimal? monthly = null, string cur = "PEN") =>
        new(type, "AVAILABLE", cur, sale, daily, weekly, monthly, true);

    [Fact]
    public void Prices_are_formatted_per_modality()
    {
        Assert.Equal(("S/ 58,000", null), Pair(Ui.Price([Op("SALE", sale: 58000)])));
        Assert.Equal(("S/ 120", "por día"), Pair(Ui.Price([Op("RENT", daily: 120)])));
        Assert.Equal(("S/ 65", "por día"), Pair(Ui.Price([Op("WORK_RENT", daily: 65)])));
        Assert.Equal(("US$ 12,500", null), Pair(Ui.Price([Op("SALE", sale: 12500, cur: "USD")])));
        Assert.Equal(("S/ 750", "por semana"), Pair(Ui.Price([Op("RENT", weekly: 750)])));
        Assert.Equal(("S/ 1,750", "por mes"), Pair(Ui.Price([Op("RENT", monthly: 1750)])));
        Assert.Null(Ui.Price([]));
    }

    [Fact]
    public void The_preferred_modality_is_shown_first()
    {
        var ops = new[] { Op("SALE", sale: 30000), Op("WORK_RENT", daily: 65) };
        Assert.Equal("S/ 65", Ui.Price(ops, "WORK_RENT")!.Amount);
        Assert.Equal("S/ 30,000", Ui.Price(ops)!.Amount);
    }

    [Theory]
    [InlineData("PUBLISHED", "success")] [InlineData("REJECTED", "danger")] [InlineData("PENDING_REVIEW", "warning")] [InlineData("DRAFT", "")]
    public void Status_tones(string status, string tone) => Assert.Equal(tone, Ui.Tone(status));

    [Fact]
    public void Labels_are_in_spanish_and_unknown_values_pass_through() =>
        Assert.Equal(["Nuevo", "Seminuevo", "Usado", "Para trabajar", "XYZ"],
            new[] { Ui.Condition("NEW"), Ui.Condition("SEMI_NEW"), Ui.Condition("USED"), Ui.Operation("WORK_RENT"), Ui.Operation("XYZ") });

    [Fact]
    public void Missing_photos_use_the_placeholder_and_initials_are_safe()
    {
        Assert.Equal("/images/placeholder-vehicle.svg", Ui.Img(null));
        Assert.Equal("/media/a.png", Ui.Img("/media/a.png"));
        Assert.Equal("AP", Ui.Initials("Ana Pérez Gómez"));
        Assert.Equal("?", Ui.Initials(null));
    }

    private static (string, string?) Pair(Ui.PriceView? p) => (p!.Amount, p.Suffix);
}

public class MarketplaceQueryTests
{
    [Fact]
    public void Empty_filters_only_send_paging() =>
        Assert.Equal("page=1&pageSize=12", new MarketplaceQuery().ToApiQuery());

    [Fact]
    public void Price_filters_force_a_currency_so_currencies_are_never_mixed()
    {
        Assert.Contains("currency=PEN", new MarketplaceQuery { PriceMax = 50000 }.ToApiQuery());
        Assert.Contains("currency=USD", new MarketplaceQuery { PriceMin = 1, Currency = "USD" }.ToApiQuery());
        Assert.Contains("currency=PEN", new MarketplaceQuery { Sort = "PRICE_ASC" }.ToApiQuery());
        Assert.DoesNotContain("currency", new MarketplaceQuery { Sort = "YEAR_DESC" }.ToApiQuery());
    }

    [Fact]
    public void Values_are_url_encoded_and_negotiable_is_sent_only_when_checked()
    {
        var q = new MarketplaceQuery { Q = "toyota & más", Negotiable = true, Operation = "WORK_RENT", DepartmentId = 15 };
        var s = q.ToApiQuery();
        Assert.Contains("q=toyota%20%26%20m%C3%A1s", s);
        Assert.Contains("negotiable=true", s);
        Assert.Contains("operation=WORK_RENT", s);
        Assert.DoesNotContain("negotiable", new MarketplaceQuery { Negotiable = false }.ToApiQuery());
    }
}

public class ViewModelValidationTests
{
    private static IList<ValidationResult> Validate(object o)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(o, new ValidationContext(o), results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public void Registration_requires_accepting_the_terms_and_a_business_name_for_business_accounts()
    {
        var model = new RegisterViewModel { FirstName = "A", LastName = "B", Email = "a@b.com", Password = "Abcdef12", ConfirmPassword = "Abcdef12", ProfileType = "BUSINESS" };
        // DataAnnotations evalúa primero los atributos de propiedad y solo después IValidatableObject
        Assert.Contains(Validate(model), e => e.MemberNames.Contains("AcceptTerms"));
        model.AcceptTerms = true;
        Assert.Contains(Validate(model), e => e.MemberNames.Contains("BusinessName"));
        model.BusinessName = "Mi Negocio";
        Assert.Empty(Validate(model));
    }

    [Fact]
    public void Password_confirmation_must_match() =>
        Assert.Contains(Validate(new ChangePasswordViewModel { CurrentPassword = "x", NewPassword = "Abcdef12", ConfirmNewPassword = "otra" }), e => e.MemberNames.Contains("ConfirmNewPassword"));
}

public sealed class FileSessionStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ml-sessions-" + Guid.NewGuid().ToString("N"));
    private readonly FileSessionStore _store;

    private sealed class Env : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Test"; public string ApplicationName { get; set; } = "t";
        public string ContentRootPath { get; set; } = Path.GetTempPath(); public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    public FileSessionStoreTests() =>
        _store = new FileSessionStore(Options.Create(new SessionStoreOptions { Path = _dir }), DataProtectionProvider.Create(new DirectoryInfo(_dir + "-keys")), new Env(), TimeProvider.System);

    [Fact]
    public async Task Tokens_round_trip_encrypted_and_can_be_deleted()
    {
        var tokens = new SessionTokens("access-token-xyz", DateTime.UtcNow.AddMinutes(15), "refresh-token-xyz", DateTime.UtcNow.AddDays(7));
        var id = await _store.CreateAsync(tokens, default);
        Assert.Matches("^[A-Za-z0-9_-]{43}$", id);
        Assert.Equal(tokens, await _store.GetAsync(id, default));
        // el contenido en disco no contiene los tokens en claro
        var raw = await File.ReadAllTextAsync(Directory.GetFiles(_dir).Single());
        Assert.DoesNotContain("access-token-xyz", raw);
        Assert.DoesNotContain("refresh-token-xyz", raw);
        await _store.DeleteAsync(id, default);
        Assert.Null(await _store.GetAsync(id, default));
    }

    [Theory]
    [InlineData("../../etc/passwd")] [InlineData("..\\..\\windows\\win.ini")] [InlineData("abc")] [InlineData("")]
    public async Task Malicious_or_malformed_session_ids_are_rejected(string id) => Assert.Null(await _store.GetAsync(id, default));

    [Fact]
    public async Task Expired_sessions_are_discarded()
    {
        var id = await _store.CreateAsync(new SessionTokens("a", DateTime.UtcNow, "r", DateTime.UtcNow.AddSeconds(-1)), default);
        Assert.Null(await _store.GetAsync(id, default));
    }

    public void Dispose() { try { Directory.Delete(_dir, true); Directory.Delete(_dir + "-keys", true); } catch { } }
}

/// <summary>Arranca el sitio real (sin API disponible) para verificar seguridad y degradación elegante.</summary>
public sealed class WebAppTests : IClassFixture<WebAppTests.Factory>
{
    public sealed class Factory : WebApplicationFactory<Program>
    {
        public Factory()
        {
            Environment.SetEnvironmentVariable("Api__BaseUrl", "http://127.0.0.1:9/");   // puerto inexistente: la API "está caída"
            Environment.SetEnvironmentVariable("Sessions__Path", Path.Combine(Path.GetTempPath(), "ml-web-tests-" + Guid.NewGuid().ToString("N")));
            Environment.SetEnvironmentVariable("DataProtection__KeysPath", Path.Combine(Path.GetTempPath(), "ml-web-keys-" + Guid.NewGuid().ToString("N")));
        }
    }

    private readonly HttpClient _client;
    public WebAppTests(Factory f) => _client = f.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [Theory]
    [InlineData("/panel")] [InlineData("/panel/publish")] [InlineData("/panel/listings")] [InlineData("/admin")] [InlineData("/admin/plans")]
    public async Task Private_areas_redirect_anonymous_users_to_login(string url)
    {
        var res = await _client.GetAsync(url);
        Assert.Equal(HttpStatusCode.Redirect, res.StatusCode);
        Assert.StartsWith("/ingresar", res.Headers.Location!.OriginalString.Replace("http://localhost", ""));
    }

    [Fact]
    public async Task Pages_send_security_headers_and_a_strict_csp()
    {
        var res = await _client.GetAsync("/ingresar");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("nosniff", res.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", res.Headers.GetValues("X-Frame-Options").Single());
        var csp = res.Headers.GetValues("Content-Security-Policy").Single();
        Assert.Contains("script-src 'self'", csp); Assert.DoesNotContain("unsafe-inline", csp); Assert.Contains("frame-ancestors 'none'", csp);
        var html = await res.Content.ReadAsStringAsync();
        Assert.Contains("__RequestVerificationToken", html);                // formularios con antiforgery
        Assert.DoesNotContain(" onclick=", html); Assert.DoesNotContain(" style=\"", html);   // sin manejadores ni estilos en línea (CSP)
    }

    [Fact]
    public async Task Posts_without_antiforgery_token_are_rejected_even_on_error_pages() =>
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PostAsync("/ingresar", new FormUrlEncodedContent(new Dictionary<string, string> { ["Email"] = "a@b.com", ["Password"] = "x" }))).StatusCode);

    [Fact]
    public async Task Home_and_plans_degrade_gracefully_when_the_api_is_down()
    {
        var home = await _client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, home.StatusCode);
        Assert.Contains("Tu próximo vehículo, tu próxima oportunidad.", await home.Content.ReadAsStringAsync());
        // /planes necesita la API: debe mostrar una página de error amable, nunca una traza
        var plans = await _client.GetAsync("/planes");
        var body = WebUtility.HtmlDecode(await plans.Content.ReadAsStringAsync());
        Assert.True((int)plans.StatusCode is 502 or 503, plans.StatusCode.ToString());
        Assert.True(body.Contains("No pudimos cargar la información"), body[..Math.Min(body.Length, 600)]);
        Assert.DoesNotContain("Exception", body);
    }

    [Fact]
    public async Task Media_proxy_rejects_traversal_and_non_image_paths()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/media/..%2f..%2fappsettings.json")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/media/secret.txt")).StatusCode);
    }

    [Fact]
    public async Task Ajax_actions_require_a_session() =>
        Assert.True((await _client.PostAsync($"/vehiculos/{Guid.NewGuid()}/favorito", new StringContent(""))).StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Redirect or HttpStatusCode.Unauthorized);

    [Fact]
    public async Task Health_endpoint_is_public() => Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/health")).StatusCode);
}
