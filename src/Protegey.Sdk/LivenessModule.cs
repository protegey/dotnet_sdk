namespace Protegey.Sdk;

/// <summary>
/// 3D liveness and re-verification sessions — <c>/partner-api/liveness/sessions</c>. Sandbox or
/// live is decided by which API key you use.
///
/// <para>A separate, paid product, billed per check where liveness was proven. KYC is unaffected
/// and stays free.</para>
///
/// <para>Two modes (UPPERCASE on the wire): <c>LIVENESS</c> — a selfie-only 3D liveness check;
/// <c>REVERIFICATION</c> — the same liveness check, then a 3D:3D match against the face of that
/// customer's latest KYC verification. <c>status</c>: <c>PENDING</c>, <c>COMPLETED</c>,
/// <c>EXPIRED</c>.</para>
/// </summary>
public sealed class LivenessModule
{
    private readonly Client _http;

    internal LivenessModule(Client http)
    {
        _http = http;
    }

    /// <summary>
    /// Starts a <c>LIVENESS</c> session (selfie-only 3D liveness check). Returns
    /// <c>{sessionId, mode, status, externalUserId, url, captureUrl, expiresInMinutes}</c> —
    /// <c>url</c> is the hosted capture page, single-use and valid for 30 minutes; send it to (or
    /// open it for) your end user.
    ///
    /// <para><paramref name="externalUserId"/> is optional and only included when non-null.</para>
    /// </summary>
    public Task<Dictionary<string, object?>> StartSessionAsync(string? externalUserId = null, CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object?> { ["mode"] = "LIVENESS" };
        if (externalUserId is not null)
        {
            body["externalUserId"] = externalUserId;
        }
        return _http.PostAsync("/partner-api/liveness/sessions", body, cancellationToken);
    }

    /// <summary>
    /// Starts a <c>REVERIFICATION</c> session: a 3D liveness check, then a 3D:3D match against the
    /// face of <paramref name="externalUserId"/>'s latest KYC verification — or of
    /// <paramref name="referenceVerificationId"/> when given (only included when non-null). Same
    /// response as <see cref="StartSessionAsync"/>.
    ///
    /// <para>When that customer has no KYC face on record the API answers 400 and this throws a
    /// <see cref="ProtegeyApiException"/> whose <c>ErrorCode</c> is
    /// <see cref="LivenessErrorCodes.No3DEnrollment"/> — run a KYC session for them first.</para>
    /// </summary>
    public Task<Dictionary<string, object?>> StartReverificationAsync(string externalUserId, string? referenceVerificationId = null, CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object?>
        {
            ["mode"] = "REVERIFICATION",
            ["externalUserId"] = externalUserId,
        };
        if (referenceVerificationId is not null)
        {
            body["referenceVerificationId"] = referenceVerificationId;
        }
        return _http.PostAsync("/partner-api/liveness/sessions", body, cancellationToken);
    }

    /// <summary>
    /// <c>GET /partner-api/liveness/sessions/{sessionId}</c> — <c>id</c>, <c>sessionId</c>,
    /// <c>mode</c>, <c>status</c>, <c>externalUserId</c>, <c>referenceEnrollmentId</c>,
    /// <c>livenessProven</c> (bool or null), <c>matchLevel</c> (number or null: 0 = different
    /// person, 15 = same person), <c>matched</c> (bool or null, reverification only),
    /// <c>duplicateFaceEnrollmentIds</c> (not currently populated), <c>attempts</c>,
    /// <c>isSandbox</c>, <c>completedAt</c>, <c>createdAt</c>.
    ///
    /// <para>Billing: <c>billableChecks</c>, <c>amount</c> (decimal string, not a number),
    /// <c>currency</c> (<c>"USD"</c>) and <c>checks[]</c> — one per attempt, each <c>{id,
    /// livenessProven, matchLevel, billable, unitPrice (decimal string), currency,
    /// createdAt}</c>.</para>
    /// </summary>
    public Task<Dictionary<string, object?>> GetSessionAsync(string sessionId, CancellationToken cancellationToken = default)
        => _http.GetAsync("/partner-api/liveness/sessions/" + Client.Segment(sessionId), null, cancellationToken);

    /// <summary>
    /// <c>GET /partner-api/liveness/sessions</c> — paginated
    /// (<c>{data, total, page, limit, totalPages}</c>), each row shaped like
    /// <see cref="GetSessionAsync"/>. Optional query keys: <c>page</c>, <c>limit</c> (1–100),
    /// <c>externalUserId</c>, <c>mode</c> (<c>"LIVENESS"</c>|<c>"REVERIFICATION"</c>).
    /// </summary>
    public Task<Dictionary<string, object?>> ListSessionsAsync(Dictionary<string, object?>? query = null, CancellationToken cancellationToken = default)
        => _http.GetAsync("/partner-api/liveness/sessions", query, cancellationToken);
}

/// <summary>Values of <see cref="ProtegeyApiException.ErrorCode"/> sent by the liveness endpoints.</summary>
public static class LivenessErrorCodes
{
    /// <summary>HTTP 400 on <see cref="LivenessModule.StartReverificationAsync"/>: no 3D face record
    /// (from a KYC verification) exists for that user, so there is nothing to match against.</summary>
    public const string No3DEnrollment = "NO_3D_ENROLLMENT";
}
