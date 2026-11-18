namespace Protegey.Sdk;

public class ProtegeyApiException : Exception
{
    /// <summary>HTTP status code, or 0 when the request never got a response (network error).</summary>
    public int Status { get; }

    /// <summary>
    /// Machine-readable code the API sends on some 403s: <c>"PARTNER_SUSPENDED"</c> or
    /// <c>"PARTNER_CONTRACT_REQUIRED"</c>. Null for every other error.
    /// </summary>
    public string? ErrorCode { get; }

    /// <summary>The parsed JSON error body, when there was one.</summary>
    public Dictionary<string, object?>? Body { get; }

    public ProtegeyApiException(int status, string message) : this(status, message, null, null)
    {
    }

    public ProtegeyApiException(int status, string message, string? errorCode, Dictionary<string, object?>? body) : base(message)
    {
        Status = status;
        ErrorCode = errorCode;
        Body = body;
    }
}
