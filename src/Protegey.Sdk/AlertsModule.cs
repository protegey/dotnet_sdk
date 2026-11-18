namespace Protegey.Sdk;

/// <summary>
/// Monitoring alerts — <c>/partner-api/alerts</c>.
///
/// <para>Wire values: <c>status</c> — <c>OPEN</c>, <c>CONFIRMED</c>, <c>MORE_INFO_REQUESTED</c>,
/// <c>DISMISSED</c>; <c>disposition</c> — <c>CONFIRMED_FRAUD</c>, <c>FALSE_POSITIVE</c>,
/// <c>NO_ACTION</c>, <c>SAR_FILED</c>, <c>ESCALATED</c> or null; <c>decisionVerdict</c> —
/// <c>BLOCK</c>, <c>STEP_UP</c>, <c>ESCALATE</c>, <c>ALERT</c> or null; <c>ruleSeverity</c> stays
/// lowercase (<c>review</c>|<c>block</c>|<c>step_up</c>|<c>escalate</c>).</para>
///
/// <para>List and get only see alerts of the key's mode (sandbox/live); update, convert-to-case
/// and flag-transaction act on any of your alerts/transactions by id.</para>
/// </summary>
public sealed class AlertsModule
{
    private readonly Client _http;

    internal AlertsModule(Client http)
    {
        _http = http;
    }

    /// <summary>
    /// <c>GET /partner-api/alerts</c> — paginated, newest first. Optional query keys:
    /// <c>page</c>, <c>limit</c>, <c>status</c>, <c>disposition</c>, <c>alertNumber</c>,
    /// <c>ruleCode</c>, <c>externalCustomerId</c>, <c>transactionId</c>, <c>dateFrom</c>,
    /// <c>dateTo</c>.
    ///
    /// <para>The API only accepts <c>status</c>/<c>disposition</c> filters in lowercase; the SDK
    /// lowercases them for you, so <c>"OPEN"</c> works.</para>
    /// </summary>
    public Task<Dictionary<string, object?>> ListAsync(Dictionary<string, object?>? query = null, CancellationToken cancellationToken = default)
    {
        var q = query is null ? null : new Dictionary<string, object?>(query);
        if (q is not null)
        {
            foreach (var key in new[] { "status", "disposition" })
            {
                if (q.TryGetValue(key, out var value) && value is string s)
                {
                    q[key] = s.ToLowerInvariant();
                }
            }
        }
        return _http.GetAsync("/partner-api/alerts", q, cancellationToken);
    }

    /// <summary><c>GET /partner-api/alerts/{id}</c> — the alert with rule context and its <c>transaction</c> snapshot (or null).</summary>
    public Task<Dictionary<string, object?>> GetAsync(string id, CancellationToken cancellationToken = default)
        => _http.GetAsync("/partner-api/alerts/" + Client.Segment(id), null, cancellationToken);

    /// <summary>
    /// <c>PATCH /partner-api/alerts/{id}</c> — every key optional: <c>status</c>,
    /// <c>disposition</c> (null clears it), <c>assignedToUserId</c> (a user of your organization,
    /// or null), <c>investigationNotes</c>, <c>dueAt</c> (ISO 8601 or null). Always fires
    /// <c>alert.status_changed</c>. Returns the updated alert.
    /// </summary>
    public Task<Dictionary<string, object?>> UpdateAsync(string id, Dictionary<string, object?> fields, CancellationToken cancellationToken = default)
        => _http.PatchAsync("/partner-api/alerts/" + Client.Segment(id), fields, cancellationToken);

    /// <summary>
    /// <c>POST /partner-api/alerts/{id}/convert-to-case</c> — opens an investigation case and sets
    /// the alert's disposition to <c>ESCALATED</c>. Returns <c>{alert, case}</c>.
    /// </summary>
    public Task<Dictionary<string, object?>> ConvertToCaseAsync(string id, CancellationToken cancellationToken = default)
        => _http.PostAsync("/partner-api/alerts/" + Client.Segment(id) + "/convert-to-case", null, cancellationToken);

    /// <summary>
    /// <c>POST /partner-api/alerts/flag-transaction</c> — manually raise an alert on a
    /// transaction. <paramref name="transactionId"/> must be Protegey's <c>transactionId</c>
    /// (not your external id). Idempotent: returns the existing open alert if there is one.
    /// </summary>
    public Task<Dictionary<string, object?>> FlagTransactionAsync(string transactionId, CancellationToken cancellationToken = default)
        => _http.PostAsync("/partner-api/alerts/flag-transaction", new Dictionary<string, object?> { ["transactionId"] = transactionId }, cancellationToken);
}
