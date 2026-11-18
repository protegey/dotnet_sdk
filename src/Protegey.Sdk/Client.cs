using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;

namespace Protegey.Sdk;

/// <summary>
/// Thin HTTP wrapper shared by every SDK module — one place that knows about auth, base URL and
/// error shape. Accepts an injectable <see cref="HttpClient"/> so tests can point it at a local
/// test server instead of making a real network call.
///
/// <para><c>baseUrl</c> is deliberately required, with NO built-in default: confirm the current
/// value with Protegey before you ship (it can differ between environments and change
/// independently of this package's version).</para>
/// </summary>
internal sealed class Client
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new ObjectToInferredTypesConverter() },
    };

    private readonly string _apiKey;
    private readonly string _baseUrl;
    private readonly HttpClient _http;

    public Client(string apiKey, string baseUrl, HttpClient? httpClient = null)
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            throw new ArgumentException("Protegey: apiKey is required", nameof(apiKey));
        }
        if (string.IsNullOrEmpty(baseUrl))
        {
            throw new ArgumentException(
                "Protegey: baseUrl is required — point it at your Protegey API environment (e.g. https://api.protegey.com)",
                nameof(baseUrl));
        }

        _apiKey = apiKey;
        _baseUrl = baseUrl.TrimEnd('/');
        _http = httpClient ?? new HttpClient();
    }

    public Task<Dictionary<string, object?>> PostAsync(string path, Dictionary<string, object?>? body = null, CancellationToken cancellationToken = default)
        => SendJsonAsync(HttpMethod.Post, path, body ?? new Dictionary<string, object?>(), cancellationToken);

    public Task<Dictionary<string, object?>> PatchAsync(string path, Dictionary<string, object?> body, CancellationToken cancellationToken = default)
        => SendJsonAsync(HttpMethod.Patch, path, body, cancellationToken);

    public async Task<Dictionary<string, object?>> GetAsync(string path, Dictionary<string, object?>? query = null, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, _baseUrl + path + BuildQuery(query));
        request.Headers.Add("x-api-key", _apiKey);
        return await SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Escapes one path segment (an id) — <c>Uri.EscapeDataString</c>, not
    /// <c>WebUtility.UrlEncode</c>, which is form encoding (a space becomes "+").</summary>
    public static string Segment(string value) => Uri.EscapeDataString(value);

    private async Task<Dictionary<string, object?>> SendJsonAsync(HttpMethod method, string path, Dictionary<string, object?> body, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, _baseUrl + path)
        {
            Content = JsonContent.Create(body, options: JsonOptions),
        };
        request.Headers.Add("x-api-key", _apiKey);
        return await SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private static string BuildQuery(Dictionary<string, object?>? query)
    {
        if (query is null || query.Count == 0)
        {
            return "";
        }

        var parts = new List<string>();
        foreach (var (key, value) in query)
        {
            if (value is null)
            {
                continue;
            }
            var text = value switch
            {
                string s => s,
                bool b => b ? "true" : "false",
                DateTime dt => (dt.Kind == DateTimeKind.Unspecified ? dt : dt.ToUniversalTime()).ToString("o", CultureInfo.InvariantCulture),
                DateTimeOffset dto => dto.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture),
                IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
                _ => value.ToString() ?? "",
            };
            parts.Add(Uri.EscapeDataString(key) + "=" + Uri.EscapeDataString(text));
        }
        return parts.Count == 0 ? "" : "?" + string.Join("&", parts);
    }

    private async Task<Dictionary<string, object?>> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException e)
        {
            throw new ProtegeyApiException(0, e.Message);
        }

        var raw = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        Dictionary<string, object?>? data;
        try
        {
            data = JsonSerializer.Deserialize<Dictionary<string, object?>>(raw, JsonOptions);
        }
        catch (JsonException)
        {
            data = null;
        }

        if (!response.IsSuccessStatusCode)
        {
            var rawMessage = data?.GetValueOrDefault("message");
            string message = rawMessage switch
            {
                List<object?> list => string.Join(", ", list.Select(m => m?.ToString())),
                not null => rawMessage.ToString()!,
                null => "Request failed",
            };
            // The 403 suspension / missing-contract guards send {errorCode, message} with no
            // statusCode — surface errorCode so callers can branch on it without string matching.
            var errorCode = data?.GetValueOrDefault("errorCode") as string;
            throw new ProtegeyApiException((int)response.StatusCode, message, errorCode, data);
        }

        return data ?? new Dictionary<string, object?>();
    }
}
