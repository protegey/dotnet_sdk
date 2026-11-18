namespace Protegey.Sdk;

/// <summary>
/// Identity verification sessions — <c>/partner-api/kyc/sessions</c>. Sandbox or live is decided
/// by which API key you use.
///
/// <para>Wire values of <c>status</c>: <c>NOT_STARTED</c>, <c>IN_PROGRESS</c>, <c>AWAITING_USER</c>,
/// <c>IN_REVIEW</c>, <c>APPROVED</c>, <c>DECLINED</c>, <c>RESUBMITTED</c>, <c>ABANDONED</c>,
/// <c>EXPIRED</c>, <c>KYC_EXPIRED</c>. <c>outcome</c>: <c>SUCCESS</c>, <c>MANUAL</c>, <c>FAILED</c>
/// or null.</para>
/// </summary>
public sealed class KycModule
{
    private readonly Client _http;

    internal KycModule(Client http)
    {
        _http = http;
    }

    /// <summary>
    /// <c>GET /partner-api/kyc/sessions</c> — paginated
    /// (<c>{data, total, page, limit, totalPages}</c>). Optional query keys: <c>page</c>,
    /// <c>limit</c> (1–100), <c>status</c>, <c>search</c> (matches fullName or externalUserId).
    ///
    /// <para>The API only accepts <c>status</c> in its internal Title Case form
    /// (<c>"In Review"</c>) and rejects the UPPER_SNAKE_CASE form it returns; pass either and the
    /// SDK converts it (<c>"IN_REVIEW"</c> → <c>"In Review"</c>).</para>
    /// </summary>
    public Task<Dictionary<string, object?>> ListSessionsAsync(Dictionary<string, object?>? query = null, CancellationToken cancellationToken = default)
    {
        var q = query is null ? null : new Dictionary<string, object?>(query);
        if (q is not null && q.TryGetValue("status", out var status) && status is string s)
        {
            q["status"] = ToTitleCase(s);
        }
        return _http.GetAsync("/partner-api/kyc/sessions", q, cancellationToken);
    }

    /// <summary>
    /// Starts an identity verification session for one of your end users. Returns
    /// <c>{sessionId, url}</c> — <c>url</c> is the hosted capture page, single-use and valid for
    /// 30 minutes.
    ///
    /// <para><paramref name="email"/> and <paramref name="phone"/> are optional, never
    /// Protegey-verified — trusted as given — and feed identity-resolution's cross-partner
    /// matching. They're only included in the request when non-null.</para>
    /// </summary>
    public Task<Dictionary<string, object?>> StartSessionAsync(string externalUserId, string? email = null, string? phone = null, CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object?> { ["externalUserId"] = externalUserId };
        if (email is not null)
        {
            body["email"] = email;
        }
        if (phone is not null)
        {
            body["phone"] = phone;
        }
        return _http.PostAsync("/partner-api/kyc/sessions", body, cancellationToken);
    }

    /// <summary>
    /// <c>GET /partner-api/kyc/sessions/{sessionId}</c> — full session: <c>status</c>,
    /// <c>outcome</c>, <c>decision</c> (null until a result exists), extracted identity fields,
    /// scores and base64 photos (<c>documentPhoto</c>, <c>documentBackPhoto</c>,
    /// <c>selfiePhoto</c>, <c>documentFaceCrop</c>; may arrive a couple of minutes after the
    /// decision).
    ///
    /// <para>Also the reliable way to correlate a <c>kyc.status_changed</c> webhook: that payload's
    /// <c>sessionId</c> is always null, so match on <c>externalUserId</c> and then poll here.</para>
    /// </summary>
    public Task<Dictionary<string, object?>> GetSessionAsync(string sessionId, CancellationToken cancellationToken = default)
        => _http.GetAsync("/partner-api/kyc/sessions/" + Client.Segment(sessionId), null, cancellationToken);

    /// <summary>
    /// <c>PATCH /partner-api/kyc/sessions/{sessionId}</c> — correct extracted fields and/or override
    /// the status. Optional keys: <c>fullName</c>, <c>externalUserId</c>, <c>country</c>,
    /// <c>documentType</c>, <c>idDocumentNumber</c>, <c>sex</c>, <c>dateOfBirth</c>,
    /// <c>placeOfBirth</c>, <c>profession</c>, <c>dateOfIssue</c>, <c>dateOfExpiration</c>,
    /// <c>status</c> (<c>"APPROVED"</c>|<c>"DECLINED"</c>|<c>"IN_REVIEW"</c>), <c>reviewNote</c>.
    /// Returns <c>{sessionId, externalUserId, status, outcome, decision}</c>. No webhook fires for
    /// a manual override.
    /// </summary>
    public Task<Dictionary<string, object?>> UpdateSessionAsync(string sessionId, Dictionary<string, object?> fields, CancellationToken cancellationToken = default)
        => _http.PatchAsync("/partner-api/kyc/sessions/" + Client.Segment(sessionId), fields, cancellationToken);

    /// <summary>
    /// <c>POST /partner-api/kyc/sessions/{sessionId}/resend</c> — issues a fresh 30-minute capture
    /// link and sets the status to <c>RESUBMITTED</c>. Returns <c>{sessionId, url}</c>.
    /// </summary>
    public Task<Dictionary<string, object?>> ResendSessionAsync(string sessionId, CancellationToken cancellationToken = default)
        => _http.PostAsync("/partner-api/kyc/sessions/" + Client.Segment(sessionId) + "/resend", null, cancellationToken);

    internal static string ToTitleCase(string status)
    {
        var words = status.Split(new[] { '_', ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(w => char.ToUpperInvariant(w[0]) + w[1..].ToLowerInvariant());
        return string.Join(" ", words);
    }
}
