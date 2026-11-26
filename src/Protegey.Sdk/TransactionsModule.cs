namespace Protegey.Sdk;

/// <summary>
/// Transaction monitoring — <c>/partner-api/transactions</c>. Sandbox or live is decided by
/// which API key you use (sandbox: 100 transactions/day).
///
/// <para>Wire values: <c>decision</c> — <c>CLEAR</c>|<c>REVIEW</c>|<c>BLOCKED</c>;
/// <c>deviceAction</c> — <c>ALLOW</c>|<c>SOFT_CHALLENGE</c>|<c>HARD_CHALLENGE</c>|<c>BLOCK</c> or
/// null; <c>direction</c> — <c>DEBIT</c>|<c>CREDIT</c>.</para>
/// </summary>
public sealed class TransactionsModule
{
    private readonly Client _http;

    internal TransactionsModule(Client http)
    {
        _http = http;
    }

    /// <summary>
    /// Thin, faithful mapping onto <c>POST /partner-api/transactions</c> — all validation and
    /// business logic stays server-side. Returns
    /// <c>{transactionId, decision, riskScore, deviceAction, alerts[]}</c>.
    ///
    /// <para>Required keys: <c>externalTransactionId</c> (idempotency key — a duplicate answers
    /// 409), <c>externalCustomerId</c>, <c>direction</c> (<c>"DEBIT"</c>|<c>"CREDIT"</c>, exact
    /// uppercase), <c>amount</c>, <c>transactionType</c>, <c>occurredAt</c> (ISO 8601 — the SDK
    /// fills in "now" when you omit it).</para>
    ///
    /// <para>Optional: <c>currency</c> (default <c>XOF</c>), <c>customerName</c>, <c>segment</c>
    /// (<c>"KYC1"</c>|<c>"KYC2"</c>|<c>"AGENT"</c>|<c>"SUPER_AGENT"</c>|<c>"MERCHANT"</c>|
    /// <c>"CORPORATE"</c>, exact uppercase), <c>country</c>, <c>isPep</c>,
    /// <c>counterpartyExternalId</c>, <c>channel</c> (<c>"branch"</c>|<c>"atm"</c>|<c>"pos"</c>|
    /// <c>"online"</c>|<c>"mobile_app"</c>|<c>"ussd"</c>|<c>"agent"</c>|<c>"api"</c>|
    /// <c>"call_center"</c>), <c>counterpartyInstitutionCode</c>, <c>counterpartyCountry</c>,
    /// <c>isCash</c>, <c>visitorId</c> (device id from a Protegey client SDK; when omitted the
    /// API matches a recent device event for this customer), <c>deviceAttributes</c> (same shape
    /// as <see cref="DeviceEventsModule.RecordAsync"/>), <c>devicePhoneNumber</c>,
    /// <c>lastPasswordChangeAt</c> / <c>lastEmailChangeAt</c> / <c>lastPhoneChangeAt</c>
    /// (ISO 8601 — lets a rule flag a large transaction right after a credential change or SIM
    /// swap), <c>accountBalanceBefore</c>, <c>location</c> — the device's GPS fix:
    /// <c>{latitude, longitude, accuracyMeters?, capturedAt? (ISO 8601), isMocked?}</c>.</para>
    ///
    /// <para>The response (and <see cref="GetAsync"/>) also carries <c>location</c>, null when none
    /// was sent: <c>{latitude, longitude, accuracyMeters, isMocked, gpsIpDistanceKm,
    /// impliedTravelSpeedKmh, isNewGpsArea}</c> — coordinates rounded to about 100 m; every field but
    /// <c>isMocked</c> can be null.</para>
    /// </summary>
    public Task<Dictionary<string, object?>> ReportAsync(Dictionary<string, object?> input, CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object?>(input);
        // DateTime (not DateTimeOffset) with Kind=Utc is what makes "o" end in "Z" rather than
        // "+00:00" — matches the ISO-8601 shape the backend (and every other SDK) already sends.
        body.TryAdd("occurredAt", DateTime.UtcNow.ToString("o"));
        return _http.PostAsync("/partner-api/transactions", body, cancellationToken);
    }

    /// <summary>
    /// <c>GET /partner-api/transactions</c> — paginated, newest <c>occurredAt</c> first. Optional
    /// query keys: <c>page</c>, <c>limit</c>, <c>direction</c>, <c>decision</c>,
    /// <c>externalCustomerId</c>, <c>dateFrom</c>, <c>dateTo</c> (ISO 8601 or
    /// <see cref="DateTime"/>). Note <c>amount</c> comes back as a decimal <em>string</em>
    /// (<c>"15000.00"</c>).
    /// </summary>
    public Task<Dictionary<string, object?>> ListAsync(Dictionary<string, object?>? query = null, CancellationToken cancellationToken = default)
        => _http.GetAsync("/partner-api/transactions", query, cancellationToken);

    /// <summary>
    /// <c>GET /partner-api/transactions/{id}</c> — <paramref name="id"/> is either Protegey's
    /// <c>transactionId</c> or your own <c>externalTransactionId</c>. Returns the decision and up
    /// to 50 linked alerts (no amount/currency/direction), and <c>location</c> (see
    /// <see cref="ReportAsync"/>).
    /// </summary>
    public Task<Dictionary<string, object?>> GetAsync(string id, CancellationToken cancellationToken = default)
        => _http.GetAsync("/partner-api/transactions/" + Client.Segment(id), null, cancellationToken);
}
