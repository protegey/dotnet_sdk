using System.Security.Cryptography;
using System.Text;

namespace Protegey.Sdk;

/// <summary>
/// Verifies the HMAC-SHA256 signature Protegey attaches to every outbound webhook delivery, so
/// you can trust that a request claiming to be from Protegey actually is.
///
/// <para>Protegey signs <c>"{timestamp}.{rawBody}"</c> with your webhook secret (HMAC-SHA256, hex
/// digest) and sends it as the <c>X-Signature</c> header, alongside the same <c>timestamp</c>
/// (unix seconds) as <c>X-Timestamp</c>. Pass the <em>raw</em> request body — not a
/// re-encoded/re-serialized version of it, which can produce a different byte sequence and
/// always fail to match.</para>
///
/// <para>Deliveries are retried up to 5 times over about 3 hours, so the same event can arrive
/// more than once — deduplicate on the payload's <c>eventId</c>. The event name is in the
/// payload's <c>type</c> field (see <see cref="WebhookEventTypes"/>); there is no event-type
/// header.</para>
/// </summary>
public static class WebhookVerifier
{
    public static bool Verify(string payload, string timestamp, string signature, string secret)
    {
        if (string.IsNullOrEmpty(signature) || string.IsNullOrEmpty(timestamp) || string.IsNullOrEmpty(secret) || payload is null)
        {
            return false;
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var rawHmac = hmac.ComputeHash(Encoding.UTF8.GetBytes($"{timestamp}.{payload}"));
        // ToHexStringLower was only added in .NET 9 — ToHexString (lowercased) targets net8.0 too.
        var expected = Convert.ToHexString(rawHmac).ToLowerInvariant();

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(signature.Trim().ToLowerInvariant()));
    }
}

/// <summary>Values of the <c>type</c> field on Protegey webhook payloads.</summary>
public static class WebhookEventTypes
{
    /// <summary>A biometric result was applied. Payload: <c>externalUserId</c>, <c>status</c>
    /// (<c>APPROVED</c>|<c>IN_REVIEW</c>|<c>DECLINED</c>), <c>decision</c>. Its <c>sessionId</c> is
    /// always null — correlate on <c>externalUserId</c>. Not sent for a manual PATCH.</summary>
    public const string KycStatusChanged = "kyc.status_changed";

    /// <summary>A KYB application was decided. Payload: <c>clientId</c>, <c>status</c>
    /// (<c>ACTIVE</c>|<c>REJECTED</c>|<c>MORE_INFO_REQUIRED</c>), <c>submissionStatus</c>
    /// (lowercase), <c>decision</c> (<c>APPROVE</c>|<c>REJECT</c>|<c>REQUEST_MORE_INFO</c>).</summary>
    public const string KybStatusChanged = "kyb.status_changed";

    /// <summary>Payload: <c>alertId</c>, <c>ruleCode</c>, <c>externalCustomerId</c>,
    /// <c>transactionId</c>, <c>status</c>, <c>triggeredAt</c>.</summary>
    public const string AlertCreated = "alert.created";

    /// <summary>Sent on every alert update and convert-to-case. Payload: <c>alertId</c>,
    /// <c>ruleCode</c>, <c>externalCustomerId</c>, <c>status</c>.</summary>
    public const string AlertStatusChanged = "alert.status_changed";

    /// <summary>Payload: <c>transactionId</c>, <c>externalTransactionId</c>,
    /// <c>externalCustomerId</c>, <c>amount</c> (a number here), <c>currency</c>,
    /// <c>riskScore</c>, <c>ruleDecision</c>, <c>deviceAction</c>, <c>alertIds</c>,
    /// <c>occurredAt</c>.</summary>
    public const string TransactionBlocked = "transaction.blocked";

    /// <summary>Test delivery triggered from the portal. Payload: <c>message</c>, <c>sentAt</c>.</summary>
    public const string WebhookTest = "webhook.test";
}
