using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using MotorLink.API.Authorization;
using MotorLink.API.Middleware;
using MotorLink.Application;
using MotorLink.Application.Options;
using MotorLink.Domain.Entities;
using MotorLink.Infrastructure;
using MotorLink.Infrastructure.Persistence;
using Serilog;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

var builder = WebApplication.CreateBuilder(args);

// ---- Logging estructurado (Serilog). Nunca se registran contraseñas ni tokens. ----
builder.Host.UseSerilog((ctx, services, cfg) =>
{
    cfg.ReadFrom.Configuration(ctx.Configuration).Enrich.FromLogContext();
    if (ctx.HostingEnvironment.IsDevelopment()) cfg.WriteTo.Console();
    else cfg.WriteTo.Console(new Serilog.Formatting.Compact.RenderedCompactJsonFormatter());
});

// ---- Capas ----
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// ---- MVC / JSON / ProblemDetails ----
builder.Services.AddProblemDetails();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<MotorLink.Application.Common.ICurrentUser, MotorLink.API.Infrastructure.HttpCurrentUser>();
builder.Services.AddHostedService<MotorLink.API.Infrastructure.MaintenanceWorker>();
builder.Services.AddControllers(o => o.ModelBinderProviders.Insert(0, new MotorLink.API.Infrastructure.SnakeCaseEnumBinderProvider())).AddJsonOptions(o =>
{
    // Enumeraciones como texto en MAYÚSCULAS (PERSONAL, BUSINESS, ...)
    o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper));
});

// ---- Autenticación JWT: valida firma, emisor, audiencia y expiración ----
var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
if (jwt.Key.Length < 32)
    throw new InvalidOperationException("Jwt:Key debe tener al menos 32 caracteres (variable de entorno Jwt__Key).");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false;
        o.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            NameClaimType = "name",
            RoleClaimType = "role"
        };
    });

// ---- Autorización: políticas, fallback autenticado y propiedad de recursos ----
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
    .AddPolicy(Policies.AdminOnly, p => p.RequireRole(RoleNames.Admin))
    .AddPolicy(Policies.User, p => p.RequireRole(RoleNames.User, RoleNames.Admin));
builder.Services.AddSingleton<IAuthorizationHandler, ResourceOwnerHandler>();

// ---- Rate limiting para endpoints de autenticación (por IP) ----
var authPermit = builder.Configuration.GetValue("RateLimiting:AuthPermitLimit", 10);
var publicWritePermit = builder.Configuration.GetValue("RateLimiting:PublicWritePermitLimit", 60);
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy(Policies.AuthRateLimit, ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = authPermit, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    o.AddPolicy(Policies.PublicWriteRateLimit, ctx => RateLimitPartition.GetFixedWindowLimiter(
        "pw:" + (ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown"),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = publicWritePermit, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    o.OnRejected = async (ctx, ct) =>
    {
        ctx.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await ctx.HttpContext.Response.WriteAsJsonAsync(new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Status = 429,
            Title = "Demasiadas solicitudes",
            Detail = "Has superado el límite de intentos. Inténtalo nuevamente en un minuto."
        }, ct);
    };
});

// ---- CORS: solo si se configuran orígenes (el frontend MVC llama server-to-server, no lo necesita) ----
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
if (corsOrigins.Length > 0)
    builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod()));

// ---- Cabeceras reenviadas (IP real detrás del frontend/proxy dentro de la red privada) ----
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
    o.ForwardLimit = 1;
});

// ---- Health checks ----
builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database", tags: ["ready"]);

// ---- Swagger / OpenAPI ----
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new OpenApiInfo { Title = "MotorLink API", Version = "v1" });
    var xml = Path.Combine(AppContext.BaseDirectory, $"{typeof(Program).Assembly.GetName().Name}.xml");
    if (File.Exists(xml)) o.IncludeXmlComments(xml);
    o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Access token JWT (15 min). Obtenerlo con POST /api/v1/auth/login."
    });
    o.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", doc)] = []
    });
});

var app = builder.Build();

app.UseForwardedHeaders();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseSerilogRequestLogging();

if (builder.Configuration.GetValue("Security:RequireHttps", false))
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

if (builder.Configuration.GetValue("Swagger:Enabled", app.Environment.IsDevelopment()))
{
    app.UseSwagger();
    app.UseSwaggerUI(o => o.SwaggerEndpoint("/swagger/v1/swagger.json", "MotorLink API v1"));
}

if (corsOrigins.Length > 0) app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Respaldo de imágenes en disco (solo si ImageStorage:LocalFallback=true y no hay Cloudinary): GET /media/{ruta}
if (app.Services.GetService<MotorLink.Infrastructure.Services.LocalDiskImageStorage>() is { } local)
{
    app.MapGet("/media/{**path}", (string path) =>
    {
        var full = local.Resolve(path);
        var type = Path.GetExtension(path).ToLowerInvariant() switch { ".png" => "image/png", ".webp" => "image/webp", ".jpg" or ".jpeg" => "image/jpeg", _ => null };
        return full is null || type is null || !File.Exists(full)
            ? Results.NotFound()
            : Results.File(full, type, enableRangeProcessing: false);
    }).AllowAnonymous();
}
// /health: estado general (incluye la base de datos). Respuesta mínima y pública; no expone detalles internos.
app.MapHealthChecks("/health", new()
{
    ResponseWriter = (ctx, report) =>
    {
        ctx.Response.ContentType = "application/json";
        return ctx.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(new { status = report.Status.ToString() }));
    }
}).AllowAnonymous();
app.MapHealthChecks("/health/live", new() { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new() { Predicate = r => r.Tags.Contains("ready") }).AllowAnonymous();

await DatabaseInitializer.InitializeAsync(app.Services,
    app.Services.GetRequiredService<IOptions<DatabaseOptions>>().Value);

await app.RunAsync();

public partial class Program;
