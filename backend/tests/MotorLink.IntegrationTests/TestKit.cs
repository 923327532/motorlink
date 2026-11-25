using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using MotorLink.Application.Auth;
using MotorLink.Infrastructure.Persistence;

namespace MotorLink.IntegrationTests;

/// <summary>Fábrica compartida por todas las pruebas de marketplace (una sola BD temporal).</summary>
[CollectionDefinition("api")]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>;

public static class Json
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper) }
    };
}

/// <summary>Cliente HTTP autenticado como un usuario concreto.</summary>
public sealed class Session
{
    public required HttpClient Client { get; init; }
    public required Guid UserId { get; init; }
    public required string Email { get; init; }
    public string? RefreshToken { get; init; }

    public Task<HttpResponseMessage> Get(string url) => Client.GetAsync(url);
    public Task<HttpResponseMessage> Post(string url, object? body = null) => Client.PostAsJsonAsync(url, body ?? new { }, Json.Options);
    public Task<HttpResponseMessage> Put(string url, object? body = null) => body is null ? Client.PutAsync(url, null) : Client.PutAsJsonAsync(url, body, Json.Options);
    public Task<HttpResponseMessage> Patch(string url, object? body = null) => Client.PatchAsJsonAsync(url, body ?? new { }, Json.Options);
    public Task<HttpResponseMessage> Delete(string url) => Client.DeleteAsync(url);

    public async Task<T> Read<T>(Task<HttpResponseMessage> response, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var r = await response;
        var text = await r.Content.ReadAsStringAsync();
        Assert.True(r.StatusCode == expected, $"Esperado {(int)expected} pero fue {(int)r.StatusCode}: {text}");
        return JsonSerializer.Deserialize<T>(text, Json.Options)!;
    }

    public async Task<JsonElement> Json_(Task<HttpResponseMessage> response, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var r = await response;
        var text = await r.Content.ReadAsStringAsync();
        Assert.True(r.StatusCode == expected, $"Esperado {(int)expected} pero fue {(int)r.StatusCode}: {text}");
        return JsonDocument.Parse(text).RootElement.Clone();
    }

    public static async Task Expect(Task<HttpResponseMessage> response, HttpStatusCode expected)
    {
        var r = await response;
        Assert.True(r.StatusCode == expected, $"Esperado {(int)expected} pero fue {(int)r.StatusCode}: {await r.Content.ReadAsStringAsync()}");
    }
}

public sealed class TestApi(ApiFactory factory)
{
    public ApiFactory Factory => factory;
    private static int _counter;

    public HttpClient Anonymous() => factory.CreateClient();

    public Session AnonymousSession() => new() { Client = factory.CreateClient(), UserId = Guid.Empty, Email = "" };

    public async Task<Session> NewUserAsync(string profileType = "PERSONAL")
    {
        var email = $"user{Interlocked.Increment(ref _counter)}_{Guid.NewGuid():N}@test.com";
        var client = factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email, password = "Abcdef12", firstName = "Ana", lastName = "Perez", profileType,
            businessName = profileType == "BUSINESS" ? "Autos Ana" : null
        });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var auth = (await res.Content.ReadFromJsonAsync<AuthResponse>(Json.Options))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return new Session { Client = client, UserId = auth.User.Id, Email = email, RefreshToken = auth.RefreshToken };
    }

    public async Task<Session> AdminAsync()
    {
        var client = factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = ApiFactory.AdminEmail, password = ApiFactory.AdminPassword });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var auth = (await res.Content.ReadFromJsonAsync<AuthResponse>(Json.Options))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return new Session { Client = client, UserId = auth.User.Id, Email = ApiFactory.AdminEmail };
    }

    // ---------- datos de prueba ----------
    public static string RandomPlate() => "T" + Guid.NewGuid().ToString("N")[..5].ToUpperInvariant();

    public static object VehicleBody(string? plate = null, int typeId = 1, int brandId = 1, int modelId = 1, int year = 2020, string condition = "USED",
        int mileage = 30000, int? doors = 4, int? seats = 5, string fuel = "GASOLINE", int? load = null, int? cc = 1500) => new
    {
        vehicleTypeId = typeId, brandId, modelId, version = "1.5 GLi", manufactureYear = year, modelYear = year,
        plate = plate ?? RandomPlate(), color = "Blanco", mileage, condition, previousOwners = condition == "NEW" ? 0 : 1,
        fuelType = fuel, transmission = "AUTOMATIC", engineDisplacementCc = cc, doors, seats, loadCapacityKg = load,
        hasAirConditioning = true, hasGps = false, hasInsurance = true, hasValidSoat = true, hasValidTechnicalReview = true,
        description = "Vehículo en excelente estado"
    };

    public static object Listing(Guid vehicleId, object[]? operations = null, string title = "Toyota Yaris impecable", int districtId = 150122,
        int provinceId = 1501, int departmentId = 15, string advertiser = "PERSONAL") => new
    {
        vehicleId, title, description = "Listo para transferir", advertiserType = advertiser,
        businessName = advertiser == "BUSINESS" ? "Autos Ana SAC" : null,
        phoneNumber = "987654321", whatsAppNumber = "987654321", contactHours = "9:00 - 18:00",
        departmentId, provinceId, districtId, locationReference = "Cerca al parque Kennedy",
        operations = operations ?? [Sale(25000)]
    };

    public static object Sale(decimal price, string currency = "PEN", bool negotiable = true) =>
        new { operationType = "SALE", currency, salePrice = price, isNegotiable = negotiable };

    public static object Rent(decimal daily, string currency = "PEN") =>
        new { operationType = "RENT", currency, dailyRate = daily, weeklyRate = daily * 6, guarantee = 500m, isNegotiable = true };

    public static object WorkRent(decimal daily, string currency = "PEN") => new
    {
        operationType = "WORK_RENT", currency, dailyRate = daily, weeklyRate = daily * 6, monthlyRate = daily * 25, guarantee = 800m,
        isNegotiable = true, additionalConditions = "Entrega y devolución con tanque lleno",
        workRent = new
        {
            allowedUse = "TAXI", shift = "FULL", minExperienceYears = 2, licenseType = "A-IIb", workZone = "Lima Metropolitana",
            monthlyKmLimit = 6000, fuelPaidBy = "DRIVER", maintenancePaidBy = "OWNER", insurancePaidBy = "OWNER",
            driverRequirements = "Antecedentes policiales y penales"
        }
    };

    /// <summary>Foto PNG mínima (firma real + relleno): suficiente para validar por contenido.</summary>
    public static byte[] PngBytes(int size = 64)
    {
        var b = new byte[Math.Max(size, 16)];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(b, 0);
        return b;
    }

    public static MultipartFormDataContent FileContent(byte[] bytes, string contentType, string fileName = "foto.png")
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "file", fileName);
        return form;
    }

    public async Task<Guid> CreateVehicleAsync(Session s, object? body = null)
    {
        var v = await s.Json_(s.Post("/api/v1/vehicles", body ?? VehicleBody()), HttpStatusCode.Created);
        return v.GetProperty("id").GetGuid();
    }

    public async Task UploadPhotoAsync(Session s, Guid vehicleId)
    {
        var r = await s.Client.PostAsync($"/api/v1/vehicles/{vehicleId}/images", FileContent(PngBytes(), "image/png"));
        Assert.True(r.StatusCode == HttpStatusCode.Created, await r.Content.ReadAsStringAsync());
    }

    /// <summary>Vehículo + foto + publicación en borrador.</summary>
    public async Task<(Guid VehicleId, Guid ListingId)> CreateDraftAsync(Session s, object[]? operations = null, object? vehicle = null, string title = "Toyota Yaris impecable", bool photo = true)
    {
        var vid = await CreateVehicleAsync(s, vehicle);
        if (photo) await UploadPhotoAsync(s, vid);
        var l = await s.Json_(s.Post("/api/v1/listings", Listing(vid, operations, title)), HttpStatusCode.Created);
        return (vid, l.GetProperty("id").GetGuid());
    }

    public async Task SubmitAsync(Session s, Guid listingId) =>
        await Session.Expect(s.Patch($"/api/v1/listings/{listingId}/status", new { status = "PENDING_REVIEW" }), HttpStatusCode.OK);

    public async Task<Guid> PublishAsync(Session owner, Session admin, object[]? operations = null, object? vehicle = null, string title = "Toyota Yaris impecable")
    {
        var (_, lid) = await CreateDraftAsync(owner, operations, vehicle, title);
        await SubmitAsync(owner, lid);
        await Session.Expect(admin.Post($"/api/v1/admin/listings/{lid}/approve"), HttpStatusCode.NoContent);
        return lid;
    }

    /// <summary>Ejecuta código con el contenedor de servicios de la API (p. ej. para forzar fechas o lanzar procesos).</summary>
    public async Task WithServicesAsync(Func<IServiceProvider, Task> action)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await action(scope.ServiceProvider);
    }

    /// <summary>Activa un plan de pago para el usuario (activación administrativa) sin pasar por el checkout.</summary>
    public async Task GrantPlanAsync(Session admin, Session user, string planCode, int months = 1) =>
        await Session.Expect(admin.Post("/api/v1/admin/subscriptions", new { userId = user.UserId, planCode, months }), HttpStatusCode.Created);
}
