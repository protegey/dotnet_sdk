using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace Protegey.Sdk.Tests;

public class WebhookVerifierTests
{
    private static string Sign(string timestamp, string payload, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var raw = hmac.ComputeHash(Encoding.UTF8.GetBytes($"{timestamp}.{payload}"));
        return Convert.ToHexString(raw).ToLowerInvariant();
    }

    [Fact]
    public void Verify_AcceptsACorrectlySignedPayload()
    {
        const string secret = "test_webhook_secret_not_real";
        const string timestamp = "1700000000";
        const string payload = "{\"type\":\"kyc.status_changed\",\"eventId\":\"e1\",\"status\":\"APPROVED\"}";
        var signature = Sign(timestamp, payload, secret);

        Assert.True(WebhookVerifier.Verify(payload, timestamp, signature, secret));
    }

    [Fact]
    public void Verify_RejectsATamperedPayload()
    {
        const string secret = "test_webhook_secret_not_real";
        const string timestamp = "1700000000";
        var signature = Sign(timestamp, "{\"event\":\"original\"}", secret);

        Assert.False(WebhookVerifier.Verify("{\"event\":\"tampered\"}", timestamp, signature, secret));
    }

    [Fact]
    public void Verify_RejectsAWrongSecret()
    {
        const string timestamp = "1700000000";
        const string payload = "{\"type\":\"kyc.status_changed\",\"eventId\":\"e1\",\"status\":\"APPROVED\"}";
        var signature = Sign(timestamp, payload, "whsec_correct");

        Assert.False(WebhookVerifier.Verify(payload, timestamp, signature, "whsec_wrong"));
    }

    [Fact]
    public void Verify_AcceptsAnUppercaseHexSignature()
    {
        const string secret = "secret";
        const string timestamp = "1700000000";
        const string payload = "{\"type\":\"webhook.test\"}";

        Assert.True(WebhookVerifier.Verify(payload, timestamp, Sign(timestamp, payload, secret).ToUpperInvariant(), secret));
    }

    [Fact]
    public void Verify_RejectsAMissingSignatureOrTimestamp()
    {
        const string payload = "{\"type\":\"webhook.test\"}";
        var signature = Sign("1700000000", payload, "secret");

        Assert.False(WebhookVerifier.Verify(payload, "1700000000", "", "secret"));
        Assert.False(WebhookVerifier.Verify(payload, "", signature, "secret"));
        Assert.False(WebhookVerifier.Verify(payload, "1700000001", signature, "secret"));
    }

    [Fact]
    public void EventTypes_MatchTheWireValues()
    {
        Assert.Equal("kyc.status_changed", WebhookEventTypes.KycStatusChanged);
        Assert.Equal("kyb.status_changed", WebhookEventTypes.KybStatusChanged);
        Assert.Equal("alert.created", WebhookEventTypes.AlertCreated);
        Assert.Equal("alert.status_changed", WebhookEventTypes.AlertStatusChanged);
        Assert.Equal("transaction.blocked", WebhookEventTypes.TransactionBlocked);
        Assert.Equal("webhook.test", WebhookEventTypes.WebhookTest);
    }
}
