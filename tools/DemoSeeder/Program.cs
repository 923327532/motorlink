// MotorLink · sembrador de datos de DEMOSTRACIÓN.
// Crea usuarios, vehículos con fotos generadas, publicaciones aprobadas, negociaciones y favoritos usando ÚNICAMENTE la API pública
// (no toca la base de datos). Uso: dotnet run -- [urlApi] [correoAdmin] [claveAdmin]
// Requisitos: API en Development con ImageStorage:LocalFallback=true, Billing:DemoPaymentsEnabled=true y límites de rate limiting altos.
using System.Drawing;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

var apiUrl = args.ElementAtOrDefault(0) ?? "http://localhost:5080";
var adminEmail = args.ElementAtOrDefault(1) ?? "admin@motorlink.local";
var adminPassword = args.ElementAtOrDefault(2) ?? "Admin#12345";
var seeder = new Seeder(apiUrl);
await seeder.RunAsync(adminEmail, adminPassword);

sealed record Spec(string Owner, string Title, int TypeId, int BrandId, int ModelId, string Version, int Year, string Condition, int Km, string Fuel, string Transmission,
    int? Cc, int? Doors, int Seats, int? Load, string Color, string Kind, Color Paint, int Dept, int Prov, int Dist, string Zone, string Status, bool Feature, object[] Ops);

sealed class Seeder(string apiUrl)
{
    private const string Password = "Demo#12345";
    private const string Domain = "demo.motorlink.example";
    private readonly Random _rnd = new(42);
    private HttpClient New() => new() { BaseAddress = new Uri(apiUrl), Timeout = TimeSpan.FromSeconds(60) };

    private static async Task<JsonElement> Call(HttpClient c, HttpMethod m, string path, object? body = null, bool allowFail = false)
    {
        using var req = new HttpRequestMessage(m, path);
        if (body is not null) req.Content = JsonContent.Create(body);
        using var res = await c.SendAsync(req);
        var text = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode && !allowFail) throw new InvalidOperationException($"{m} {path} -> {(int)res.StatusCode}: {text}");
        if (string.IsNullOrWhiteSpace(text)) return JsonDocument.Parse($"{{\"status\":{(int)res.StatusCode}}}").RootElement;
        var doc = JsonDocument.Parse(text).RootElement.Clone();
        return doc;
    }

    private sealed record User(string Email, HttpClient Client, Guid Id, string Name);

    private async Task<User> EnsureUser(string email, string first, string last, string profile, string? business, string? phone)
    {
        var c = New();
        var reg = await Call(c, HttpMethod.Post, "/api/v1/auth/register", new { email, password = Password, firstName = first, lastName = last, profileType = profile, businessName = business, phoneNumber = phone }, allowFail: true);
        JsonElement auth = reg;
        if (!reg.TryGetProperty("accessToken", out _))
            auth = await Call(c, HttpMethod.Post, "/api/v1/auth/login", new { email, password = Password });
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.GetProperty("accessToken").GetString());
        return new User(email, c, auth.GetProperty("user").GetProperty("id").GetGuid(), $"{first} {last}");
    }

    public async Task RunAsync(string adminEmail, string adminPassword)
    {
        Console.WriteLine($"Sembrando datos de demostración en {apiUrl} …");
        var admin = New();
        var login = await Call(admin, HttpMethod.Post, "/api/v1/auth/login", new { email = adminEmail, password = adminPassword });
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.GetProperty("accessToken").GetString());

        var carlos = await EnsureUser($"carlos.mendoza@{Domain}", "Carlos", "Mendoza", "PERSONAL", null, "987111222");
        var lucia = await EnsureUser($"lucia.paredes@{Domain}", "Lucía", "Paredes", "PERSONAL", null, "987333444");
        var rosa = await EnsureUser($"rosa.quispe@{Domain}", "Rosa", "Quispe", "PERSONAL", null, "987555666");
        var autoperu = await EnsureUser($"ventas@autoperu.{Domain}", "AutoPerú", "Ventas", "BUSINESS", "AutoPerú Motors", "987777888");
        var flota = await EnsureUser($"alquileres@flotaexpress.{Domain}", "Flota", "Express", "BUSINESS", "Flota Express", "987999000");
        var buyer = await EnsureUser($"comprador@{Domain}", "Mateo", "Rojas", "PERSONAL", null, "987123123");

        async Task Grant(User u, string plan)
        {
            var cur = await Call(u.Client, HttpMethod.Get, "/api/v1/subscriptions/current");
            if (cur.GetProperty("plan").GetProperty("code").GetString() == plan) return;
            await Call(admin, HttpMethod.Post, "/api/v1/admin/subscriptions", new { userId = u.Id, planCode = plan, months = 12 });
            Console.WriteLine($"  plan {plan} -> {u.Email}");
        }
        await Grant(carlos, "BASIC"); await Grant(lucia, "BASIC"); await Grant(autoperu, "PROFESSIONAL"); await Grant(flota, "BUSINESS");

        var users = new Dictionary<string, User> { ["carlos"] = carlos, ["lucia"] = lucia, ["rosa"] = rosa, ["autoperu"] = autoperu, ["flota"] = flota };
        var listingIds = new Dictionary<string, Guid>();

        foreach (var (key, owner) in users)
        {
            var has = await Call(owner.Client, HttpMethod.Get, "/api/v1/vehicles?pageSize=1");
            if (has.GetProperty("totalCount").GetInt32() > 0) { Console.WriteLine($"  {key}: ya tiene vehículos, se omite"); continue; }

            foreach (var spec in Specs().Where(s => s.Owner == key))
            {
                var vehicle = await Call(owner.Client, HttpMethod.Post, "/api/v1/vehicles", new
                {
                    vehicleTypeId = spec.TypeId, brandId = spec.BrandId, modelId = spec.ModelId, version = spec.Version, manufactureYear = spec.Year, modelYear = spec.Year,
                    plate = Plate(), color = spec.Color, mileage = spec.Km, condition = spec.Condition, previousOwners = spec.Condition == "NEW" ? 0 : 1, fuelType = spec.Fuel,
                    transmission = spec.Transmission, engineDisplacementCc = spec.Cc, doors = spec.Doors, seats = spec.Seats, loadCapacityKg = spec.Load,
                    hasAirConditioning = spec.Kind != "moto" && spec.Kind != "mototaxi", hasGps = _rnd.Next(2) == 0, hasInsurance = true, hasValidSoat = true, hasValidTechnicalReview = true,
                    description = $"{spec.Title}. Mantenimiento al día, documentos en regla y listo para transferir. Datos declarados por el propietario."
                });
                var vid = vehicle.GetProperty("id").GetGuid();

                for (var v = 0; v < 3; v++)
                {
                    var png = PhotoGenerator.Create(spec.Kind, spec.Paint, v, spec.Title);
                    using var form = new MultipartFormDataContent();
                    var file = new ByteArrayContent(png); file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
                    form.Add(file, "file", $"demo-{v}.png");
                    using var up = await owner.Client.PostAsync($"/api/v1/vehicles/{vid}/images", form);
                    if (!up.IsSuccessStatusCode) throw new InvalidOperationException("Fallo al subir foto: " + await up.Content.ReadAsStringAsync());
                }

                var listing = await Call(owner.Client, HttpMethod.Post, "/api/v1/listings", new
                {
                    vehicleId = vid, title = spec.Title, description = $"{spec.Title}. Publicación de demostración generada para probar MotorLink.",
                    advertiserType = owner.Email.Contains("autoperu") || owner.Email.Contains("flota") ? "BUSINESS" : "PERSONAL",
                    businessName = owner.Email.Contains("autoperu") ? "AutoPerú Motors" : owner.Email.Contains("flota") ? "Flota Express" : null,
                    phoneNumber = "987654321", whatsAppNumber = "987654321", contactHours = "Lun–Sáb 9:00–19:00",
                    departmentId = spec.Dept, provinceId = spec.Prov, districtId = spec.Dist, locationReference = spec.Zone,
                    additionalConditions = "Precio sujeto a revisión mecánica. Datos declarados por el anunciante.", operations = spec.Ops
                });
                var lid = listing.GetProperty("id").GetGuid();
                listingIds[spec.Title] = lid;

                if (spec.Status != "DRAFT")
                {
                    await Call(owner.Client, HttpMethod.Patch, $"/api/v1/listings/{lid}/status", new { status = "PENDING_REVIEW" });
                    if (spec.Status == "PUBLISHED")
                    {
                        await Call(admin, HttpMethod.Post, $"/api/v1/admin/listings/{lid}/approve");
                        if (spec.Feature) await Call(owner.Client, HttpMethod.Patch, $"/api/v1/listings/{lid}/featured", new { isFeatured = true }, allowFail: true);
                    }
                }
                Console.WriteLine($"  {spec.Status,-14} {spec.Title}");
            }
        }

        // ---- Actividad: vistas, contactos, favoritos y negociaciones (si aún no hay) ----
        var anon = New();
        var existing = await Call(buyer.Client, HttpMethod.Get, "/api/v1/negotiations?pageSize=1");
        if (existing.GetProperty("totalCount").GetInt32() == 0)
        {
            var published = (await Call(anon, HttpMethod.Get, "/api/v1/listings?pageSize=50")).GetProperty("items").EnumerateArray().ToList();
            Guid Find(string part) => published.First(l => l.GetProperty("title").GetString()!.Contains(part, StringComparison.OrdinalIgnoreCase)).GetProperty("id").GetGuid();

            foreach (var l in published)
            {
                var id = l.GetProperty("id").GetGuid();
                for (var i = 0; i < _rnd.Next(6, 28); i++) await Call(anon, HttpMethod.Post, $"/api/v1/listings/{id}/views", allowFail: true);
                for (var i = 0; i < _rnd.Next(1, 7); i++) await Call(anon, HttpMethod.Post, $"/api/v1/listings/{id}/contact", new { channel = "WHATSAPP" }, allowFail: true);
            }
            foreach (var part in new[] { "Hilux", "Swift", "N300", "CR-V", "Torito" })
                await Call(buyer.Client, HttpMethod.Put, $"/api/v1/favorites/{Find(part)}", allowFail: true);

            // Acuerdo: oferta -> contrapropuesta -> aceptación
            var hilux = await Call(buyer.Client, HttpMethod.Post, "/api/v1/negotiations", new { listingId = Find("Hilux"), operationType = "SALE", amount = 120000, message = "Pago al contado. ¿Podemos cerrar en 120 mil?" });
            var hid = hilux.GetProperty("negotiation").GetProperty("id").GetGuid();
            var counter = await Call(autoperu.Client, HttpMethod.Post, $"/api/v1/negotiations/{hid}/offers", new { amount = 124000, message = "Podemos llegar a 124 mil con la transferencia incluida." });
            await Call(buyer.Client, HttpMethod.Patch, $"/api/v1/negotiations/{hid}/accept", new { offerId = counter.GetProperty("offers").EnumerateArray().Last().GetProperty("id").GetGuid() });

            // En curso: contrapropuesta esperando respuesta del comprador (alquiler para trabajar)
            var accent = await Call(buyer.Client, HttpMethod.Post, "/api/v1/negotiations", new { listingId = Find("Accent"), operationType = "WORK_RENT", periodicity = "DAILY", amount = 55, message = "Trabajaría turno completo. ¿S/ 55 diarios?" });
            await Call(carlos.Client, HttpMethod.Post, $"/api/v1/negotiations/{accent.GetProperty("negotiation").GetProperty("id").GetGuid()}/offers", new { amount = 60, message = "Lo dejo en S/ 60 por día con garantía de S/ 500." });

            // Pendientes de respuesta del propietario
            await Call(buyer.Client, HttpMethod.Post, "/api/v1/negotiations", new { listingId = Find("Yaris 2020"), operationType = "SALE", amount = 45000, message = "Interesado, ¿puedo verlo este fin de semana?" });
            await Call(buyer.Client, HttpMethod.Post, "/api/v1/negotiations", new { listingId = Find("Swift"), operationType = "WORK_RENT", periodicity = "DAILY", amount = 48, message = "Quiero trabajar con delivery de lunes a sábado." });
        }

        Console.WriteLine();
        Console.WriteLine("Listo. Cuentas de demostración (contraseña común: " + Password + "):");
        Console.WriteLine($"  Administrador      {adminEmail} / {adminPassword}");
        foreach (var u in new[] { carlos, lucia, rosa, autoperu, flota, buyer }) Console.WriteLine($"  {u.Name,-18} {u.Email}");
    }

    private string Plate() => "D" + Guid.NewGuid().ToString("N")[..5].ToUpperInvariant();

    private static object Sale(decimal p, string cur = "PEN", bool neg = true) => new { operationType = "SALE", currency = cur, salePrice = p, isNegotiable = neg };
    private static object Rent(decimal d, decimal w) => new { operationType = "RENT", currency = "PEN", dailyRate = d, weeklyRate = w, guarantee = 500, isNegotiable = true };
    private static object Work(decimal d, string use, string shift, string fuel, int exp, string zone, string lic = "A-IIb", int? km = 6000) => new
    {
        operationType = "WORK_RENT", currency = "PEN", dailyRate = d, weeklyRate = d * 6, monthlyRate = d * 25, guarantee = 800, isNegotiable = true,
        additionalConditions = "Entrega y devolución con el tanque en el mismo nivel.",
        workRent = new { allowedUse = use, shift, minExperienceYears = exp, licenseType = lic, workZone = zone, monthlyKmLimit = km, fuelPaidBy = fuel, maintenancePaidBy = "OWNER", insurancePaidBy = "OWNER", driverRequirements = "Antecedentes policiales y penales vigentes. Licencia al día." }
    };

    private static IEnumerable<Spec> Specs()
    {
        // Distritos UBIGEO (Id = código): Lima 150101, Ate 150103, La Molina 150114, Miraflores 150122, San Borja 150130, San Isidro 150131, SJL 150132, Surco 150140, Surquillo 150141
        yield return new("carlos", "Toyota Yaris 2020 en excelente estado", 1, 1, 1, "1.5 GLi", 2020, "USED", 38000, "GASOLINE", "MANUAL", 1500, 4, 5, null, "Blanco", "sedan", Color.FromArgb(235, 238, 245), 15, 1501, 150122, "Cerca al parque Kennedy", "PUBLISHED", true, [Sale(48500)]);
        yield return new("carlos", "Hyundai Accent 2019 listo para taxi", 1, 2, 6, "1.4 GL", 2019, "USED", 82000, "CNG", "MANUAL", 1400, 4, 5, null, "Plata", "sedan", Color.FromArgb(170, 178, 192), 15, 1501, 150132, "Zona Canto Grande", "PUBLISHED", false, [Sale(36900), Work(65, "TAXI", "FULL", "DRIVER", 2, "Lima Metropolitana")]);
        yield return new("carlos", "Toyota Corolla 2018 (borrador)", 1, 1, 2, "1.8 XLi", 2018, "USED", 64000, "GASOLINE", "AUTOMATIC", 1800, 4, 5, null, "Gris", "sedan", Color.FromArgb(120, 130, 150), 15, 1501, 150140, "Santiago de Surco", "DRAFT", false, [Sale(52000)]);

        yield return new("lucia", "Kia Rio 2021 económico y confiable", 1, 3, 10, "1.4 LX", 2021, "USED", 29000, "GASOLINE", "AUTOMATIC", 1400, 4, 5, null, "Rojo", "hatch", Color.FromArgb(200, 40, 50), 15, 1501, 150140, "Cerca a Av. Primavera", "PUBLISHED", true, [Sale(12500, "USD")]);
        yield return new("lucia", "Toyota Avanza 2021 7 asientos para alquiler", 2, 1, 5, "1.5 G", 2021, "USED", 41000, "GASOLINE", "MANUAL", 1500, 5, 7, null, "Azul", "suv", Color.FromArgb(40, 80, 170), 7, 701, 70101, "Callao cercado", "PUBLISHED", false, [Rent(120, 750)]);
        yield return new("lucia", "Volkswagen Gol 2018 (en revisión)", 1, 9, 32, "1.6 Trendline", 2018, "USED", 70000, "GASOLINE", "MANUAL", 1600, 4, 5, null, "Negro", "hatch", Color.FromArgb(30, 34, 44), 15, 1501, 150141, "Surquillo", "PENDING", false, [Sale(23500)]);

        yield return new("autoperu", "Toyota Hilux 4x4 2022 diésel automática", 4, 1, 3, "2.8 SRV 4x4", 2022, "SEMI_NEW", 8500, "DIESEL", "AUTOMATIC", 2800, 4, 5, 1000, "Gris oscuro", "pickup", Color.FromArgb(70, 78, 92), 15, 1501, 150131, "Av. Javier Prado", "PUBLISHED", true, [Sale(128000)]);
        yield return new("autoperu", "Honda CR-V 2018 full equipo", 4, 7, 28, "2.4 EX", 2018, "USED", 56000, "GASOLINE", "AUTOMATIC", 2400, 5, 5, null, "Blanco perla", "suv", Color.FromArgb(240, 240, 235), 15, 1501, 150114, "La Molina", "PUBLISHED", true, [Sale(74000)]);
        yield return new("autoperu", "Mazda 3 2022 seminuevo", 1, 8, 30, "2.0 Sport", 2022, "SEMI_NEW", 12000, "GASOLINE", "AUTOMATIC", 2000, 5, 5, null, "Rojo vino", "hatch", Color.FromArgb(150, 28, 48), 15, 1501, 150130, "San Borja Sur", "PUBLISHED", true, [Sale(82000)]);
        yield return new("autoperu", "Nissan Frontier 2020 diésel 4x4", 4, 4, 16, "2.3 LE 4x4", 2020, "USED", 47000, "DIESEL", "MANUAL", 2300, 4, 5, 1000, "Azul acero", "pickup", Color.FromArgb(52, 92, 140), 4, 401, 40101, "Arequipa centro", "PUBLISHED", false, [Sale(21000, "USD")]);

        yield return new("flota", "Suzuki Swift 2019 para delivery", 1, 5, 18, "1.2 GL", 2019, "USED", 61000, "GASOLINE", "MANUAL", 1200, 4, 5, null, "Naranja", "hatch", Color.FromArgb(230, 120, 30), 15, 1501, 150141, "Surquillo", "PUBLISHED", true, [Work(55, "DELIVERY", "DAY", "DRIVER", 1, "Lima Moderna", "A-I", 5000)]);
        yield return new("flota", "Chevrolet N300 2020 furgoneta de carga", 5, 6, 24, "1.2 Cargo", 2020, "USED", 54000, "GASOLINE", "MANUAL", 1200, 3, 2, 900, "Blanco", "van", Color.FromArgb(235, 238, 245), 15, 1501, 150103, "Ate, Santa Anita", "PUBLISHED", true, [Work(90, "CARGO", "FULL", "DRIVER", 3, "Lima y Callao", "A-IIb", 7000), Rent(110, 650)]);
        yield return new("flota", "Bajaj Torito RE 2022 mototaxi", 8, 11, 41, "RE Compact", 2022, "USED", 15000, "GASOLINE", "MANUAL", null, null, 3, null, "Verde", "mototaxi", Color.FromArgb(40, 140, 80), 20, 2001, 200101, "Piura centro", "PUBLISHED", false, [Work(35, "TRANSPORT", "FULL", "DRIVER", 1, "Piura y Castilla", "B-IIb", 4000)]);
        yield return new("flota", "Toyota Yaris 2019 GNV para taxi", 1, 1, 1, "1.3 XLi", 2019, "USED", 77000, "CNG", "MANUAL", 1300, 4, 5, null, "Amarillo", "hatch", Color.FromArgb(235, 196, 40), 13, 1301, 130101, "Trujillo centro", "PUBLISHED", true, [Work(70, "TAXI", "FULL", "DRIVER", 2, "Trujillo", "A-IIb", 6500)]);
        yield return new("flota", "Hino 300 2019 camión liviano", 9, 12, 42, "Serie 300", 2019, "USED", 98000, "DIESEL", "MANUAL", 4000, 2, 3, 4500, "Blanco", "truck", Color.FromArgb(235, 238, 245), 14, 1401, 140101, "Chiclayo", "PUBLISHED", false, [Rent(350, 2100), Sale(95000)]);

        yield return new("rosa", "Honda CB 190R 2023 casi nueva", 7, 7, 39, "CB 190R", 2023, "SEMI_NEW", 2500, "GASOLINE", "MANUAL", 190, null, 2, null, "Rojo", "moto", Color.FromArgb(210, 30, 40), 15, 1501, 150101, "Cercado de Lima", "PUBLISHED", false, [Sale(9800)]);
        yield return new("rosa", "Hyundai H1 2020 van familiar", 5, 2, 38, "2.5 CRDi", 2020, "USED", 52000, "DIESEL", "AUTOMATIC", 2500, 4, 9, 1100, "Plata", "van", Color.FromArgb(180, 186, 198), 15, 1501, 150140, "Santiago de Surco", "PUBLISHED", false, [Sale(24000, "USD")]);
    }
}
