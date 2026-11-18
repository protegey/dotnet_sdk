namespace Protegey.Sdk;

/// <summary>Sanctions and PEP screening — <c>POST /partner-api/sanctions/screen</c>.</summary>
public sealed class SanctionsModule
{
    private readonly Client _http;

    internal SanctionsModule(Client http)
    {
        _http = http;
    }

    /// <summary>
    /// Screens a name. <paramref name="type"/> is <c>"person"</c> or <c>"business"</c> (the API
    /// only accepts lowercase; the SDK lowercases it for you), or null to omit it.
    ///
    /// <para>Returns <c>{decision, score, pepMatch, matches[], provider, providers,
    /// defaultListChecked, degraded}</c> (plus <c>fallbackReason</c> when degraded).
    /// <c>decision</c> is <c>BLOCKED</c>|<c>REVIEW</c>|<c>CLEAR</c>. Each match has <c>source</c>
    /// (lowercase: <c>nigsac</c>, <c>ofac</c>, <c>eu</c>, <c>un</c>, <c>au</c>, <c>dfat</c>,
    /// <c>custom</c>, <c>dow_jones</c>), <c>matchedOn</c> (<c>name</c>|<c>alias</c>) and
    /// <c>isPep</c>.</para>
    /// </summary>
    public Task<Dictionary<string, object?>> ScreenAsync(string name, string? type = null, CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object?> { ["name"] = name };
        if (type is not null)
        {
            body["type"] = type.ToLowerInvariant();
        }
        return _http.PostAsync("/partner-api/sanctions/screen", body, cancellationToken);
    }
}
