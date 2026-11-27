namespace Protegey.Sdk;

/// <summary>
/// Identity verification sessions — <c>/partner-api/kyc/sessions</c>. Sandbox or live is decided
/// by which API key you use.
///
/// <para>Wire values of <c>status</c>: <c>NOT_STARTED</c>, <c>IN_PROGRESS</c>, <c>AWAITING_USER</c>,
/// <c>IN_REVIEW</c>, <c>APPROVED</c>, <c>DECLINED</c>, <c>RESUBMITTED</c>, <c>ABANDONED</c>,
/// <c>EXPIRED</c>, <c>KYC_EXPIRED</c>. <c>outcome</c>: <c>SUCCESS</c>, <c>MANUAL</c>, <c>FAILED</c>
/// or null. <c>recommendation</c>: <c>APPROVE</c>, <c>REVIEW</c>, <c>REJECT</c> or null —
/// Protegey's own verdict, never changed by a manual override; <c>status</c> is the final
/// decision.</para>
///
/// <para><c>images</c> (list and detail): <c>{documentFront, documentBack, selfie,
/// documentFaceCrop}</c>, each <c>{url, expiresAt}</c> or null — pre-signed links valid 1 hour and
/// re-issued on every GET, so fetch again rather than storing them.</para>
///
/// <para>Capture links are single-use and valid until the returned <c>expiresAt</c> — your
/// capture-link lifetime from the portal's KYC settings (30 min, 1 h, 24 h, 3 days or 7 days;
/// 24 h by default). A session requested through <see cref="KybModule.RequestKycVerificationAsync"/>
/// carries <c>kybClientId</c> (null otherwise) and its link is valid at least 7 days.</para>
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
    /// <c>limit</c> (1–100), <c>status</c>, <c>search</c> (partial match on fullName or
    /// externalUserId), <c>externalUserId</c> (exact, case-sensitive match on your own id).
    ///
    /// <para>Each row: <c>sessionId</c>, <c>externalUserId</c>, <c>status</c>, <c>outcome</c>,
    /// <c>recommendation</c>, <c>fullName</c>, <c>documentType</c>, <c>country</c>,
    /// <c>kybClientId</c> (or null), <c>createdAt</c>, <c>images</c> (no base64 photos, no
    /// scores).</para>
    ///
    /// <para>The SDK sends <c>status</c> in Title Case (<c>"IN_REVIEW"</c> → <c>"In Review"</c>),
    /// which every API version accepts; current versions accept the UPPER_SNAKE_CASE form too.</para>
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
    /// <c>{sessionId, url, expiresAt}</c> — <c>url</c> is the hosted capture page, single-use and
    /// valid until <c>expiresAt</c> (24 h by default, see above).
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
    /// <c>outcome</c>, <c>recommendation</c>, <c>decision</c> (null until a result exists),
    /// extracted identity fields, scores, <c>images</c> (pre-signed links, see above) and the older
    /// base64 photos (<c>documentPhoto</c>, <c>documentBackPhoto</c>, <c>selfiePhoto</c>,
    /// <c>documentFaceCrop</c>), kept for backward compatibility. Also <c>kybClientId</c> (or null),
    /// <c>kybNameMatch</c> (<c>MATCH</c>|<c>MISMATCH</c>|null — the name on the document compared
    /// with the KYB-expected name) and <c>decision.documentFullName</c> (may be absent or null).
    /// <c>decision.identityResolution.networkFlag</c>, when present, may carry
    /// <c>groupEntities</c> (the entities of your own partner group that reported this person).
    ///
    /// <para>Partner groups only: <c>groupPresence</c> lists the same person's KYC at sister
    /// entities of your group (<c>[{entity, status, createdAt, matchedOn}]</c>, <c>matchedOn</c>
    /// among <c>FACE</c>|<c>GOVERNMENT_ID</c>|<c>PHONE</c>|<c>EMAIL</c>), or is null when none or
    /// when you are in no group — information for your reviewer, being a client of two sister
    /// entities is normal.</para>
    ///
    /// <para>Polling fallback for the <c>kyc.status_changed</c> webhook, whose <c>sessionId</c> is
    /// this same id.</para>
    /// </summary>
    public Task<Dictionary<string, object?>> GetSessionAsync(string sessionId, CancellationToken cancellationToken = default)
        => _http.GetAsync("/partner-api/kyc/sessions/" + Client.Segment(sessionId), null, cancellationToken);

    /// <summary>
    /// <c>PATCH /partner-api/kyc/sessions/{sessionId}</c> — correct extracted fields and/or override
    /// the status. Optional keys: <c>fullName</c>, <c>externalUserId</c>, <c>country</c>,
    /// <c>documentType</c>, <c>idDocumentNumber</c>, <c>sex</c>, <c>dateOfBirth</c>,
    /// <c>placeOfBirth</c>, <c>profession</c>, <c>dateOfIssue</c>, <c>dateOfExpiration</c>,
    /// <c>status</c> (<c>"APPROVED"</c>|<c>"DECLINED"</c>|<c>"IN_REVIEW"</c>), <c>reviewNote</c>.
    /// Returns <c>{sessionId, externalUserId, kybClientId, status, outcome, recommendation,
    /// decision}</c>. A
    /// status change is recorded as <c>decision.manualReview</c> (<c>reviewedBy</c> =
    /// <c>"Partner API"</c>, <c>reviewedAt</c>, <c>previousStatus</c> / <c>newStatus</c> in
    /// UPPER_SNAKE_CASE, <c>note</c>) and sends a <c>kyc.status_changed</c> webhook.
    /// </summary>
    public Task<Dictionary<string, object?>> UpdateSessionAsync(string sessionId, Dictionary<string, object?> fields, CancellationToken cancellationToken = default)
        => _http.PatchAsync("/partner-api/kyc/sessions/" + Client.Segment(sessionId), fields, cancellationToken);

    /// <summary>
    /// <c>POST /partner-api/kyc/sessions/{sessionId}/resend</c> — issues a fresh capture link and
    /// sets the status to <c>RESUBMITTED</c>. Returns <c>{sessionId, url, expiresAt}</c>.
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
