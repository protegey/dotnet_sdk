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
        const string secret = "whsec_test123";
        const string timestamp = "1700000000";
        const string payload = "{\"event\":\"kyc.session.updated\"}";
        var signature = Sign(timestamp, payload, secret);

        Assert.True(WebhookVerifier.Verify(payload, timestamp, signature, secret));
    }

    [Fact]
    public void Verify_RejectsATamperedPayload()
    {
        const string secret = "whsec_test123";
        const string timestamp = "1700000000";
        var signature = Sign(timestamp, "{\"event\":\"original\"}", secret);

        Assert.False(WebhookVerifier.Verify("{\"event\":\"tampered\"}", timestamp, signature, secret));
    }

    [Fact]
    public void Verify_RejectsAWrongSecret()
    {
        const string timestamp = "1700000000";
        const string payload = "{\"event\":\"kyc.session.updated\"}";
        var signature = Sign(timestamp, payload, "whsec_correct");

        Assert.False(WebhookVerifier.Verify(payload, timestamp, signature, "whsec_wrong"));
    }
}
