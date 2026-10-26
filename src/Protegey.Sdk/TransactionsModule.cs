namespace Protegey.Sdk;

public sealed class TransactionsModule
{
    private readonly Client _http;

    internal TransactionsModule(Client http)
    {
        _http = http;
    }

    /// <summary>
    /// Thin, faithful mapping onto <c>POST /partner-api/transactions</c> — all validation and
    /// business logic stays server-side.
    ///
    /// <para>Required keys: <c>externalTransactionId</c>, <c>externalCustomerId</c>,
    /// <c>direction</c> (<c>"DEBIT"</c>|<c>"CREDIT"</c>), <c>amount</c>, <c>transactionType</c>.
    /// Optional: <c>currency</c>, <c>counterpartyExternalId</c>, <c>isCash</c>,
    /// <c>occurredAt</c> (defaults to now), <c>segment</c>, <c>country</c>, <c>isPep</c>,
    /// <c>channel</c> (<c>"branch"</c>|<c>"atm"</c>|<c>"pos"</c>|<c>"online"</c>|
    /// <c>"mobile_app"</c>|<c>"ussd"</c>|<c>"agent"</c>|<c>"api"</c>|<c>"call_center"</c>),
    /// <c>counterpartyInstitutionCode</c> (SWIFT/BIC or a local bank/MNO code),
    /// <c>counterpartyCountry</c> (compare against the customer's own <c>country</c> in a Pan
    /// Studio rule to flag cross-border activity), <c>lastPasswordChangeAt</c> /
    /// <c>lastEmailChangeAt</c> / <c>lastPhoneChangeAt</c> (ISO 8601 — if you track when a
    /// credential last changed, this lets a Pan Studio rule flag a large transaction shortly
    /// after, a classic account-takeover pattern; <c>lastPhoneChangeAt</c> also covers a SIM
    /// swap for telcos. Omit any of them and the corresponding rule field simply never fires —
    /// Protegey never guesses this), <c>accountBalanceBefore</c> (the account balance right
    /// before this transaction, if you track it — lets a Pan Studio rule flag a withdrawal/
    /// transfer that drains most of the balance, a classic mule-account/account-drain signal;
    /// only meaningful for a DEBIT), <c>customerName</c> (the customer's display name, as you
    /// know it — self-declared, never verified by Protegey; shows up directly in the Pan Studio
    /// transactions table instead of just the opaque <c>externalCustomerId</c>; send it once
    /// and it's remembered on this customer's later transactions too, even if you omit it on
    /// some of them).
    /// </summary>
    public Task<Dictionary<string, object?>> ReportAsync(Dictionary<string, object?> input, CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object?>(input);
        // DateTime (not DateTimeOffset) with Kind=Utc is what makes "o" end in "Z" rather than
        // "+00:00" — matches the ISO-8601 shape the backend (and every other SDK) already sends.
        body.TryAdd("occurredAt", DateTime.UtcNow.ToString("o"));
        return _http.PostAsync("/partner-api/transactions", body, cancellationToken);
    }
}
