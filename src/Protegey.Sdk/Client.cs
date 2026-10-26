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

    public async Task<Dictionary<string, object?>> PostAsync(string path, Dictionary<string, object?> body, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, _baseUrl + path)
        {
            Content = JsonContent.Create(body, options: JsonOptions),
        };
        request.Headers.Add("x-api-key", _apiKey);
        return await SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Dictionary<string, object?>> GetAsync(string path, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, _baseUrl + path);
        request.Headers.Add("x-api-key", _apiKey);
        return await SendAsync(request, cancellationToken).ConfigureAwait(false);
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
            throw new ProtegeyApiException((int)response.StatusCode, message);
        }

        return data ?? new Dictionary<string, object?>();
    }
}
