using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace MotorLink.Web.Services.Sessions;

/// <summary>Tokens de la API asociados a una sesión web. Nunca se envían al navegador.</summary>
public sealed record SessionTokens(
    string AccessToken, DateTime AccessTokenExpiresAt, string RefreshToken, DateTime RefreshTokenExpiresAt);

public interface ISessionStore
{
    Task<string> CreateAsync(SessionTokens tokens, CancellationToken ct);
    Task<SessionTokens?> GetAsync(string sessionId, CancellationToken ct);
    Task UpdateAsync(string sessionId, SessionTokens tokens, CancellationToken ct);
    Task DeleteAsync(string sessionId, CancellationToken ct);
}

public sealed class SessionStoreOptions
{
    public const string SectionName = "Sessions";
    public string Path { get; set; } = "data/sessions";
}

/// <summary>
/// Almacén persistente de sesiones en disco (volumen). Cada sesión es un archivo cifrado con ASP.NET Core Data Protection;
/// el navegador solo conoce un identificador aleatorio opaco de 256 bits (sin significado ni datos).
/// </summary>
public sealed partial class FileSessionStore : ISessionStore
{
    private readonly string _root;
    private readonly IDataProtector _protector;
    private readonly TimeProvider _clock;

    public FileSessionStore(IOptions<SessionStoreOptions> options, IDataProtectionProvider dp, IHostEnvironment env, TimeProvider clock)
    {
        _root = Path.IsPathRooted(options.Value.Path) ? options.Value.Path : Path.Combine(env.ContentRootPath, options.Value.Path);
        Directory.CreateDirectory(_root);
        _protector = dp.CreateProtector("MotorLink.Web.SessionStore.v1");
        _clock = clock;
    }

    public async Task<string> CreateAsync(SessionTokens tokens, CancellationToken ct)
    {
        var id = Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(
            System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        await WriteAsync(id, tokens, ct);
        return id;
    }

    public async Task<SessionTokens?> GetAsync(string sessionId, CancellationToken ct)
    {
        var path = PathFor(sessionId);
        if (path is null || !File.Exists(path)) return null;
        try
        {
            var protectedText = await File.ReadAllTextAsync(path, ct);
            var tokens = JsonSerializer.Deserialize<SessionTokens>(_protector.Unprotect(protectedText));
            if (tokens is null || tokens.RefreshTokenExpiresAt <= _clock.GetUtcNow().UtcDateTime)
            {
                TryDelete(path);
                return null;
            }
            return tokens;
        }
        catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException or JsonException or IOException)
        {
            return null; // archivo corrupto, manipulado o en escritura concurrente => sesión inválida
        }
    }

    public Task UpdateAsync(string sessionId, SessionTokens tokens, CancellationToken ct) => WriteAsync(sessionId, tokens, ct);

    public Task DeleteAsync(string sessionId, CancellationToken ct)
    {
        if (PathFor(sessionId) is { } path) TryDelete(path);
        return Task.CompletedTask;
    }

    private async Task WriteAsync(string id, SessionTokens tokens, CancellationToken ct)
    {
        var path = PathFor(id) ?? throw new ArgumentException("Identificador de sesión inválido.", nameof(id));
        var tmp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        await File.WriteAllTextAsync(tmp, _protector.Protect(JsonSerializer.Serialize(tokens)), ct);
        File.Move(tmp, path, overwrite: true); // escritura atómica
    }

    /// <summary>Valida el formato del id para impedir path traversal.</summary>
    private string? PathFor(string id) => IdPattern().IsMatch(id) ? Path.Combine(_root, id + ".session") : null;

    private static void TryDelete(string path) { try { File.Delete(path); } catch (IOException) { } }

    [GeneratedRegex("^[A-Za-z0-9_-]{43}$")]
    private static partial Regex IdPattern();
}
