namespace Protegey.Sdk;

/// <summary>
/// Cash deposits taken in your own system (branch, agent, ATM…) — <c>/partner-api/cash-deposits</c>.
/// A deposit is monitored like any transaction (structuring and large-cash rules, same alerts).
/// Sandbox or live is decided by which API key you use.
///
/// <para>Wire values: <c>decision</c> — <c>CLEAR</c>|<c>REVIEW</c>|<c>BLOCKED</c>; <c>channel</c>
/// is sent in any casing (e.g. <c>"BRANCH"</c> or <c>"branch"</c>) and comes back UPPER_SNAKE_CASE
/// (<c>"MOBILE_APP"</c>).</para>
/// </summary>
public sealed class CashDepositsModule
{
    private readonly Client _http;

    internal CashDepositsModule(Client http)
    {
        _http = http;
    }

    /// <summary>
    /// <c>POST /partner-api/cash-deposits</c>. Returns
    /// <c>{cashDepositId, externalTransactionId, decision, riskScore, alerts[]}</c> (alert
    /// summaries: <c>id</c>, <c>alertNumber</c>, <c>ruleCode</c>, <c>status</c>,
    /// <c>decisionVerdict</c>, <c>triggeredAt</c>).
    ///
    /// <para>Required keys: <c>externalCustomerId</c>, <c>amount</c> (&gt; 0), <c>currency</c>
    /// (ISO 4217).</para>
    ///
    /// <para>Optional: <c>externalTransactionId</c> (your reference — send it to make retries
    /// safe: reusing one answers 409; Protegey generates <c>cd_&lt;uuid&gt;</c> when omitted),
    /// <c>customerName</c>, <c>depositedAt</c> (ISO 8601, defaults to the time of the call),
    /// <c>channel</c>, <c>branchId</c>, <c>agentId</c>, <c>location</c>, <c>depositorName</c>
    /// (who brought the cash, when not the account holder), <c>metadata</c> (object).</para>
    /// </summary>
    public Task<Dictionary<string, object?>> RecordAsync(Dictionary<string, object?> input, CancellationToken cancellationToken = default)
        => _http.PostAsync("/partner-api/cash-deposits", input, cancellationToken);

    /// <summary>
    /// <c>GET /partner-api/cash-deposits</c> — paginated (<c>{data, total, page, limit,
    /// totalPages}</c>), newest <c>depositedAt</c> first. Optional query keys: <c>page</c>,
    /// <c>limit</c> (1–100), <c>dateFrom</c>, <c>dateTo</c> (ISO 8601 or <see cref="DateTime"/>,
    /// on <c>depositedAt</c>), <c>externalCustomerId</c>, <c>minAmount</c>, <c>maxAmount</c>,
    /// <c>currency</c>, <c>channel</c>, <c>decision</c>, <c>branchId</c>, <c>agentId</c>.
    ///
    /// <para>Each row: <c>cashDepositId</c>, <c>externalTransactionId</c>,
    /// <c>externalCustomerId</c>, <c>customerName</c>, <c>amount</c> (a decimal <em>string</em>,
    /// <c>"2500000.00"</c>), <c>currency</c>, <c>channel</c>, <c>branchId</c>, <c>agentId</c>,
    /// <c>location</c>, <c>depositorName</c>, <c>metadata</c>, <c>decision</c>,
    /// <c>riskScore</c>, <c>depositedAt</c>, <c>createdAt</c>.</para>
    /// </summary>
    public Task<Dictionary<string, object?>> ListAsync(Dictionary<string, object?>? query = null, CancellationToken cancellationToken = default)
        => _http.GetAsync("/partner-api/cash-deposits", query, cancellationToken);

    /// <summary>
    /// <c>GET /partner-api/cash-deposits/{id}</c> — <paramref name="id"/> is either Protegey's
    /// <c>cashDepositId</c> or your own <c>externalTransactionId</c>. Same fields as a list row,
    /// plus <c>alerts[]</c> (up to 50). 404 when neither matches.
    /// </summary>
    public Task<Dictionary<string, object?>> GetAsync(string id, CancellationToken cancellationToken = default)
        => _http.GetAsync("/partner-api/cash-deposits/" + Client.Segment(id), null, cancellationToken);
}
