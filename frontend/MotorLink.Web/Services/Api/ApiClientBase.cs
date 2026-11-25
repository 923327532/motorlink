using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace MotorLink.Web.Services.Api;

/// <summary>La API respondió 401: la sesión ya no es válida. El filtro global cierra la sesión local y envía al login.</summary>
public sealed class ApiUnauthorizedException : Exception { public ApiUnauthorizedException() : base("Sesión no válida.") { } }

/// <summary>La API respondió 403 por rol/política (sin cuerpo ProblemDetails).</summary>
public sealed class ApiForbiddenException : Exception { public ApiForbiddenException() : base("Acceso denegado.") { } }

/// <summary>Un dato imprescindible para renderizar la página no se pudo obtener.</summary>
public sealed class ApiPageException(ApiError error) : Exception(error.Message) { public ApiError Error { get; } = error; }

public static class ApiResultExtensions
{
    /// <summary>Devuelve el valor o lanza ApiPageException (mostrada por el filtro global como página de error amable).</summary>
    public static T Data<T>(this ApiResult<T> r) => r.Success ? r.Value! : throw new ApiPageException(r.Error!);
}

/// <summary>Serialización, manejo uniforme de errores (ProblemDetails) y distinción 401/403 vs errores de negocio.</summary>
public abstract class ApiClientBase(HttpClient http)
{
    protected static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    protected HttpClient Http { get; } = http;

    public static string Query(params (string Key, object? Value)[] pairs) =>
        string.Join('&', pairs.Where(p => p.Value is not null && p.Value.ToString() != "")
            .Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(Convert.ToString(p.Value, System.Globalization.CultureInfo.InvariantCulture)!)}"));

    protected static string Url(string path, string? query) => string.IsNullOrEmpty(query) ? path : $"{path}?{query}";

    protected Task<ApiResult<T>> GetAsync<T>(string url, CancellationToken ct) => SendAsync<T>(HttpMethod.Get, url, null, ct);
    protected Task<ApiResult<T>> PostAsync<T>(string url, object? body, CancellationToken ct, bool authErrors = true) => SendAsync<T>(HttpMethod.Post, url, body ?? new { }, ct, authErrors);
    protected Task<ApiResult<T>> PutAsync<T>(string url, object? body, CancellationToken ct) => SendAsync<T>(HttpMethod.Put, url, body, ct);
    protected Task<ApiResult<T>> PatchAsync<T>(string url, object? body, CancellationToken ct) => SendAsync<T>(HttpMethod.Patch, url, body ?? new { }, ct);
    protected Task<ApiResult<bool>> DeleteAsync(string url, CancellationToken ct) => SendAsync<bool>(HttpMethod.Delete, url, null, ct);

    /// <param name="authErrors">false para login/registro: allí un 401 es una credencial inválida, no una sesión caducada.</param>
    protected async Task<ApiResult<T>> SendAsync<T>(HttpMethod method, string url, object? body, CancellationToken ct, bool authErrors = true)
    {
        using var request = new HttpRequestMessage(method, url);
        if (body is not null) request.Content = JsonContent.Create(body, options: Json);
        return await ExecuteAsync<T>(request, ct, authErrors);
    }

    protected async Task<ApiResult<T>> UploadAsync<T>(string url, Stream content, string fileName, string contentType, CancellationToken ct)
    {
        using var form = new MultipartFormDataContent();
        var file = new StreamContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "file", fileName);
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = form };
        return await ExecuteAsync<T>(request, ct, true);
    }

    protected async Task<ApiResult<(byte[] Bytes, string ContentType)>> DownloadAsync(string url, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        HttpResponseMessage response;
        try { response = await Http.SendAsync(request, ct); }
        catch (HttpRequestException) { return ApiResult<(byte[], string)>.Fail(Unavailable()); }
        using (response)
        {
            if (!response.IsSuccessStatusCode) return ApiResult<(byte[], string)>.Fail(await ReadErrorAsync(response, ct, true));
            return ApiResult<(byte[], string)>.Ok((await response.Content.ReadAsByteArrayAsync(ct), response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream"));
        }
    }

    private async Task<ApiResult<T>> ExecuteAsync<T>(HttpRequestMessage request, CancellationToken ct, bool authErrors)
    {
        HttpResponseMessage response;
        try { response = await Http.SendAsync(request, ct); }
        catch (HttpRequestException) { return ApiResult<T>.Fail(Unavailable()); }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested) { return ApiResult<T>.Fail(Unavailable()); }

        using (response)
        {
            if (response.IsSuccessStatusCode)
            {
                if (typeof(T) == typeof(bool)) return ApiResult<T>.Ok((T)(object)true);
                var value = await response.Content.ReadFromJsonAsync<T>(Json, ct);
                return ApiResult<T>.Ok(value!);
            }
            return ApiResult<T>.Fail(await ReadErrorAsync(response, ct, authErrors));
        }
    }

    private static ApiError Unavailable() => new(503, "Servicio no disponible", "No fue posible comunicarse con el servidor. Inténtalo nuevamente en unos minutos.", new Dictionary<string, string[]>());

    private static async Task<ApiError> ReadErrorAsync(HttpResponseMessage response, CancellationToken ct, bool authErrors)
    {
        var status = (int)response.StatusCode;
        var text = await response.Content.ReadAsStringAsync(ct);
        var hasProblemBody = response.Content.Headers.ContentType?.MediaType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true && text.Length > 2;

        if (authErrors && status == 401) throw new ApiUnauthorizedException();
        // 403 sin ProblemDetails = rechazo de rol/política. Con ProblemDetails (p. ej. límite del plan) es un error de negocio.
        if (authErrors && status == 403 && !hasProblemBody) throw new ApiForbiddenException();

        var fieldErrors = new Dictionary<string, string[]>();
        var title = status switch
        {
            401 => "Credenciales no válidas", 403 => "Acceso denegado", 404 => "No encontrado", 423 => "Cuenta bloqueada",
            429 => "Demasiadas solicitudes", _ => "No se pudo completar la operación"
        };
        string? detail = null, code = null;

        if (hasProblemBody)
        {
            try
            {
                using var doc = JsonDocument.Parse(text);
                var root = doc.RootElement;
                if (root.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String) title = t.GetString()!;
                if (root.TryGetProperty("detail", out var d) && d.ValueKind == JsonValueKind.String) detail = d.GetString();
                if (root.TryGetProperty("code", out var c) && c.ValueKind == JsonValueKind.String) code = c.GetString();
                if (root.TryGetProperty("errors", out var errs) && errs.ValueKind == JsonValueKind.Object)
                    foreach (var p in errs.EnumerateObject())
                        fieldErrors[p.Name] = p.Value.EnumerateArray().Select(e => e.GetString() ?? "").ToArray();
            }
            catch (JsonException) { /* cuerpo no interpretable: se usa el título genérico */ }
        }
        return new ApiError(status, title, detail, fieldErrors, code);
    }
}
