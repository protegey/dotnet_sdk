namespace Protegey.Sdk;

/// <summary>
/// Shared fraud signal network — <c>POST /partner-api/shared-signal/check</c>. Requires the
/// network to be enabled in your organization settings for 7 consecutive days (400 otherwise).
/// </summary>
public sealed class SharedSignalModule
{
    private readonly Client _http;

    internal SharedSignalModule(Client http)
    {
        _http = http;
    }

    /// <summary>
    /// Checks whether any partner has reported one of these identifiers. Keys (at least one
    /// identifier required): <c>phoneNumber</c>, <c>email</c>, <c>deviceFingerprint</c> (a device
    /// <c>visitorId</c>), <c>governmentId</c>, <c>personIdentityId</c>; plus optional
    /// <c>externalCustomerId</c> — when set and a match is found, that customer's risk profile is
    /// raised.
    ///
    /// <para>Returns <c>{flagged, category, reportedDaysAgo, flagConfidence, matchCount,
    /// groupMatchCount, matches[]}</c>. Each match carries <c>category</c>,
    /// <c>reportedDaysAgo</c> and <c>groupEntity</c> (newest first) — the reporter and the reported
    /// person's details are never returned, except a sister entity of your own partner group:
    /// <c>groupEntity</c> is then its name (null for the public network, which stays anonymous)
    /// and <c>groupMatchCount</c> counts those reports (0 when you are in no group). Both are
    /// optional (absent on older API versions). <c>category</c>:
    /// <c>CONFIRMED_FRAUD</c>|<c>IDENTITY_THEFT</c>|<c>MONEY_LAUNDERING</c>|<c>OTHER</c> (null
    /// when not flagged).</para>
    /// </summary>
    public Task<Dictionary<string, object?>> CheckAsync(Dictionary<string, object?> identifiers, CancellationToken cancellationToken = default)
        => _http.PostAsync("/partner-api/shared-signal/check", identifiers, cancellationToken);
}
