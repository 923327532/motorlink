using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using MotorLink.Application.Auth;

[assembly: CollectionBehavior(DisableTestParallelization = true)] // las fábricas comparten variables de entorno

namespace MotorLink.IntegrationTests;

public sealed class AuthEndpointsTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public AuthEndpointsTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static string NewEmail() => $"user{Guid.NewGuid():N}@test.com";

    private async Task<AuthResponse> Register(string? email = null, string password = "Abcdef12", string profileType = "PERSONAL")
    {
        var res = await _client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email = email ?? NewEmail(), password, firstName = "Ana", lastName = "Perez", profileType,
            businessName = profileType == "BUSINESS" ? "Autos Ana" : null
        });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (await res.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    private HttpRequestMessage Authed(HttpMethod method, string url, string token, object? body = null)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) req.Content = JsonContent.Create(body);
        return req;
    }

    [Fact]
    public async Task Register_and_login_work_and_me_returns_user()
    {
        var email = NewEmail();
        var reg = await Register(email);
        Assert.Contains("USER", reg.User.Roles);

        var login = await _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Abcdef12" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var tokens = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;

        var me = await _client.SendAsync(Authed(HttpMethod.Get, "/api/v1/auth/me", tokens.AccessToken));
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var dto = (await me.Content.ReadFromJsonAsync<UserDto>())!;
        Assert.Equal(email, dto.Email);

        // Expiraciones: access 15 min, refresh 7 días
        Assert.InRange((tokens.AccessTokenExpiresAt - DateTime.UtcNow).TotalMinutes, 14, 15.1);
        Assert.InRange((tokens.RefreshTokenExpiresAt - DateTime.UtcNow).TotalDays, 6.9, 7.1);
    }

    [Fact]
    public async Task Protected_endpoints_reject_anonymous_and_invalid_tokens()
    {
        foreach (var url in new[] { "/api/v1/auth/me", "/api/v1/users/profile", "/api/v1/admin/users" })
            Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync(url)).StatusCode);

        var bad = await _client.SendAsync(Authed(HttpMethod.Get, "/api/v1/auth/me", "not.a.jwt"));
        Assert.Equal(HttpStatusCode.Unauthorized, bad.StatusCode);
    }

    [Fact]
    public async Task Tampered_token_is_rejected()
    {
        var reg = await Register();
        var parts = reg.AccessToken.Split('.');
        var forged = $"{parts[0]}.{parts[1]}.{new string('A', parts[2].Length)}";
        var res = await _client.SendAsync(Authed(HttpMethod.Get, "/api/v1/auth/me", forged));
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Duplicate_email_returns_409_and_weak_password_returns_400_with_field_errors()
    {
        var email = NewEmail();
        await Register(email);
        var dup = await _client.PostAsJsonAsync("/api/v1/auth/register",
            new { email, password = "Abcdef12", firstName = "A", lastName = "B" });
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);
        Assert.Equal("application/problem+json", dup.Content.Headers.ContentType?.MediaType);

        var weak = await _client.PostAsJsonAsync("/api/v1/auth/register",
            new { email = NewEmail(), password = "abcdefgh", firstName = "A", lastName = "B" });
        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);
        using var json = JsonDocument.Parse(await weak.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetProperty("errors").TryGetProperty("Password", out _));
    }

    [Fact]
    public async Task Invalid_model_returns_400_problem_details()
    {
        var res = await _client.PostAsJsonAsync("/api/v1/auth/register", new { email = "not-an-email", password = "x" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Wrong_password_returns_401_and_account_locks_after_5_failures()
    {
        var email = NewEmail();
        await Register(email);
        for (var i = 0; i < 4; i++)
            Assert.Equal(HttpStatusCode.Unauthorized,
                (await _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Wrong1234" })).StatusCode);

        // 5.º intento fallido activa el bloqueo
        var fifth = await _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Wrong1234" });
        Assert.Equal(HttpStatusCode.Locked, fifth.StatusCode);

        // Con la contraseña correcta sigue bloqueada
        var correct = await _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Abcdef12" });
        Assert.Equal(HttpStatusCode.Locked, correct.StatusCode);
    }

    [Fact]
    public async Task Refresh_rotates_tokens_and_reuse_revokes_the_whole_session()
    {
        var first = await Register();

        var r1 = await _client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = first.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, r1.StatusCode);
        var second = (await r1.Content.ReadFromJsonAsync<AuthResponse>())!;
        Assert.NotEqual(first.RefreshToken, second.RefreshToken);

        // Reutilizar el token anterior => 401 y se revoca toda la familia
        var reuse = await _client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = first.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);

        var afterReuse = await _client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = second.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, afterReuse.StatusCode);
    }

    [Fact]
    public async Task Logout_revokes_refresh_token()
    {
        var tokens = await Register();
        var logout = await _client.PostAsJsonAsync("/api/v1/auth/logout", new { refreshToken = tokens.RefreshToken });
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        var refresh = await _client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = tokens.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);

        // idempotente
        Assert.Equal(HttpStatusCode.NoContent,
            (await _client.PostAsJsonAsync("/api/v1/auth/logout", new { refreshToken = "desconocido" })).StatusCode);
    }

    [Fact]
    public async Task Logout_all_sessions_revokes_other_devices()
    {
        var email = NewEmail();
        var a = await Register(email);
        var b = (await (await _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Abcdef12" }))
            .Content.ReadFromJsonAsync<AuthResponse>())!;

        await _client.PostAsJsonAsync("/api/v1/auth/logout", new { refreshToken = a.RefreshToken, allSessions = true });

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await _client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = b.RefreshToken })).StatusCode);
    }

    [Fact]
    public async Task Change_password_validates_current_revokes_sessions_and_enables_new_password()
    {
        var email = NewEmail();
        var tokens = await Register(email);

        var wrong = await _client.SendAsync(Authed(HttpMethod.Post, "/api/v1/auth/change-password", tokens.AccessToken,
            new { currentPassword = "Incorrecta1", newPassword = "Nuevo1234" }));
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);

        var ok = await _client.SendAsync(Authed(HttpMethod.Post, "/api/v1/auth/change-password", tokens.AccessToken,
            new { currentPassword = "Abcdef12", newPassword = "Nuevo1234" }));
        Assert.Equal(HttpStatusCode.NoContent, ok.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await _client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = tokens.RefreshToken })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Abcdef12" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Nuevo1234" })).StatusCode);
    }

    [Fact]
    public async Task Roles_user_cannot_access_admin_but_admin_can()
    {
        var user = await Register();
        Assert.Equal(HttpStatusCode.Forbidden,
            (await _client.SendAsync(Authed(HttpMethod.Get, "/api/v1/admin/users", user.AccessToken))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await _client.SendAsync(Authed(HttpMethod.Get, "/api/v1/admin/reports/users-by-profile-type", user.AccessToken))).StatusCode);

        var login = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = ApiFactory.AdminEmail, password = ApiFactory.AdminPassword });
        var admin = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        Assert.Contains("ADMIN", admin.User.Roles);

        Assert.Equal(HttpStatusCode.OK,
            (await _client.SendAsync(Authed(HttpMethod.Get, "/api/v1/admin/users", admin.AccessToken))).StatusCode);
        var report = await _client.SendAsync(Authed(HttpMethod.Get, "/api/v1/admin/reports/users-by-profile-type", admin.AccessToken));
        Assert.Equal(HttpStatusCode.OK, report.StatusCode); // reporte ADO.NET
    }

    [Fact]
    public async Task Profile_can_be_read_and_updated_and_only_affects_own_user()
    {
        var a = await Register(profileType: "BUSINESS");
        var b = await Register();

        var upd = await _client.SendAsync(Authed(HttpMethod.Put, "/api/v1/users/profile", a.AccessToken, new
        {
            firstName = "Carla", lastName = "Ruiz", phoneNumber = "987654321", profileType = "BUSINESS",
            businessName = "Carla Motors", ruc = "20123456789", districtId = 150122
        }));
        Assert.Equal(HttpStatusCode.OK, upd.StatusCode);

        var getA = await _client.SendAsync(Authed(HttpMethod.Get, "/api/v1/users/profile", a.AccessToken));
        using var jsonA = JsonDocument.Parse(await getA.Content.ReadAsStringAsync());
        Assert.Equal("Carla", jsonA.RootElement.GetProperty("firstName").GetString());
        Assert.Equal("Carla Motors", jsonA.RootElement.GetProperty("business").GetProperty("businessName").GetString());

        var getB = await _client.SendAsync(Authed(HttpMethod.Get, "/api/v1/users/profile", b.AccessToken));
        using var jsonB = JsonDocument.Parse(await getB.Content.ReadAsStringAsync());
        Assert.Equal("Ana", jsonB.RootElement.GetProperty("firstName").GetString());

        // RUC duplicado => 409 ; distrito inexistente => 409
        var dup = await _client.SendAsync(Authed(HttpMethod.Put, "/api/v1/users/profile", b.AccessToken, new
        {
            firstName = "X", lastName = "Y", profileType = "BUSINESS", businessName = "Otro", ruc = "20123456789"
        }));
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);
    }

    [Fact]
    public async Task Swagger_documents_all_initial_endpoints_and_health_is_ready()
    {
        var doc = await _client.GetStringAsync("/swagger/v1/swagger.json");
        foreach (var path in new[]
                 {
                     "/api/v1/auth/register", "/api/v1/auth/login", "/api/v1/auth/refresh", "/api/v1/auth/logout",
                     "/api/v1/auth/me", "/api/v1/auth/change-password", "/api/v1/users/profile"
                 })
            Assert.Contains($"\"{path}\"", doc);

        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/health/ready")).StatusCode);
    }

    [Fact]
    public async Task Seed_data_is_present_via_migrations()
    {
        // 25 departamentos, roles y planes sembrados: verificado indirectamente por el login del admin y el reporte.
        var login = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = ApiFactory.AdminEmail, password = ApiFactory.AdminPassword });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MotorLink.Infrastructure.Persistence.AppDbContext>();
        Assert.Equal(25, db.Departments.Count());
        Assert.Equal(5, db.Plans.Count());
        Assert.Equal(2, db.Roles.Count());
        Assert.True(db.Models.Any());
        Assert.True(db.VehicleTypes.Any());
    }
}

public sealed class RateLimitTests
{
    [Fact]
    public async Task Auth_endpoints_are_rate_limited_per_ip()
    {
        using var factory = new ApiFactory(authRateLimit: 3);
        var client = factory.CreateClient();
        var codes = new List<HttpStatusCode>();
        for (var i = 0; i < 5; i++)
            codes.Add((await client.PostAsJsonAsync("/api/v1/auth/login", new { email = "x@y.com", password = "Whatever1" })).StatusCode);

        Assert.Equal(HttpStatusCode.TooManyRequests, codes[^1]);
        Assert.Equal(3, codes.Count(c => c == HttpStatusCode.Unauthorized));
    }
}
