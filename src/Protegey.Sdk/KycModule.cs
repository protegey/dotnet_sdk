namespace Protegey.Sdk;

public sealed class KycModule
{
    private readonly Client _http;

    internal KycModule(Client http)
    {
        _http = http;
    }

    /// <summary>
    /// Starts an identity verification session for one of your end users — no manual API call
    /// needed. Returns a dictionary with <c>sessionId</c> and <c>url</c> (the hosted verification
    /// link).
    /// </summary>
    public Task<Dictionary<string, object?>> StartSessionAsync(string externalUserId, CancellationToken cancellationToken = default)
    {
        return _http.PostAsync("/partner-api/kyc/sessions", new Dictionary<string, object?> { ["externalUserId"] = externalUserId }, cancellationToken);
    }

    /// <summary>
    /// Polling fallback for the webhook — call this if you're not sure a webhook delivery ever
    /// arrived (best-effort: one retry, no queue). <paramref name="sessionId"/> is the value
    /// returned by <see cref="StartSessionAsync"/>.
    /// </summary>
    public Task<Dictionary<string, object?>> GetSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        // Uri.EscapeDataString, not WebUtility.UrlEncode — the latter is form/query-string
        // encoding (encodes a space as "+", wrong in a path segment); this is a path segment.
        var encoded = Uri.EscapeDataString(sessionId);
        return _http.GetAsync("/partner-api/kyc/sessions/" + encoded, cancellationToken);
    }
}
