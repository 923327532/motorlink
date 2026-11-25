using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MotorLink.Application.Vehicles;

namespace MotorLink.IntegrationTests;

/// <summary>Almacén de imágenes en memoria: permite probar subida, borrado y fallos del proveedor sin Cloudinary.</summary>
public sealed class FakeImageStorage : IImageStorageService
{
    public Dictionary<string, string> Stored { get; } = new();
    public bool FailDeletes { get; set; }
    public bool FailUploads { get; set; }

    public Task<StoredImage> UploadAsync(Stream content, string contentType, string folder, CancellationToken ct)
    {
        if (FailUploads) throw new InvalidOperationException("fallo simulado del proveedor");
        var id = $"{folder}/{Guid.NewGuid():N}";
        lock (Stored) Stored[id] = contentType;
        return Task.FromResult(new StoredImage($"https://img.test/{id}.png", id));
    }

    public Task DeleteAsync(string publicId, CancellationToken ct)
    {
        if (FailDeletes) throw new InvalidOperationException("fallo simulado del proveedor");
        lock (Stored) Stored.Remove(publicId);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Levanta la API real contra una base SQL Server temporal (MotorLinkDB_Test_xxx) que se elimina al terminar.
/// Conexión base: variable MOTORLINK_TEST_CONNECTION o, por defecto, SQL Server Express local con autenticación integrada.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const string AdminEmail = "admin@test.local";
    public const string AdminPassword = "Admin#12345";

    private readonly string _dbName = $"MotorLinkDB_Test_{Guid.NewGuid():N}";
    private readonly string _masterConnection;
    private readonly Dictionary<string, string?> _previousEnv = new();

    public FakeImageStorage Storage { get; } = new();

    public ApiFactory() : this(1000) { }

    internal ApiFactory(int authRateLimit)
    {
        var baseConn = Environment.GetEnvironmentVariable("MOTORLINK_TEST_CONNECTION")
                       ?? @"Server=.\SQLEXPRESS;Trusted_Connection=True;TrustServerCertificate=True";
        var builder = new SqlConnectionStringBuilder(baseConn) { InitialCatalog = "master" };
        _masterConnection = builder.ConnectionString;
        builder.InitialCatalog = _dbName;

        // Variables de entorno: la configuración se lee antes de que ConfigureWebHost pueda intervenir.
        Set("ConnectionStrings__Default", builder.ConnectionString);
        Set("Jwt__Key", "integration-tests-key-0123456789-abcdefghijklmnop");
        Set("Database__MigrateOnStartup", "true");
        Set("Database__AdminEmail", AdminEmail);
        Set("Database__AdminPassword", AdminPassword);
        Set("RateLimiting__AuthPermitLimit", authRateLimit.ToString());
        Set("RateLimiting__PublicWritePermitLimit", "100000");
        Set("Maintenance__IntervalMinutes", "0");          // los procesos periódicos se invocan explícitamente en las pruebas
        Set("Billing__DemoPaymentsEnabled", "true");
    }

    private void Set(string key, string value)
    {
        _previousEnv[key] = Environment.GetEnvironmentVariable(key);
        Environment.SetEnvironmentVariable(key, value);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IImageStorageService>();
            services.AddSingleton<IImageStorageService>(Storage);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;
        try
        {
            SqlConnection.ClearAllPools();
            using var conn = new SqlConnection(_masterConnection);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"IF DB_ID(N'{_dbName}') IS NOT NULL BEGIN ALTER DATABASE [{_dbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_dbName}]; END";
            cmd.ExecuteNonQuery();
        }
        finally
        {
            foreach (var (k, v) in _previousEnv) Environment.SetEnvironmentVariable(k, v);
        }
    }
}
