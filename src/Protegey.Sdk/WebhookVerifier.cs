using System.Security.Cryptography;
using System.Text;

namespace Protegey.Sdk;

/// <summary>
/// Verifies the HMAC-SHA256 signature Protegey attaches to every outbound webhook delivery, so
/// you can trust that a request claiming to be from Protegey actually is.
///
/// <para>Protegey signs <c>"{timestamp}.{rawBody}"</c> with your webhook secret (HMAC-SHA256, hex
/// digest) and sends it as the <c>X-Signature</c> header, alongside the same <c>timestamp</c> as
/// <c>X-Timestamp</c>. Pass the <em>raw</em> request body — not a re-encoded/re-serialized version
/// of it, which can produce a different byte sequence and always fail to match.</para>
/// </summary>
public static class WebhookVerifier
{
    public static bool Verify(string payload, string timestamp, string signature, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var rawHmac = hmac.ComputeHash(Encoding.UTF8.GetBytes($"{timestamp}.{payload}"));
        // ToHexStringLower was only added in .NET 9 — ToHexString (lowercased) targets net8.0 too.
        var expected = Convert.ToHexString(rawHmac).ToLowerInvariant();

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(signature));
    }
}
