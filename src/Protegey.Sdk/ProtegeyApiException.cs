namespace Protegey.Sdk;

public class ProtegeyApiException : Exception
{
    /// <summary>HTTP status code, or 0 when the request never got a response (network error).</summary>
    public int Status { get; }

    /// <summary>
    /// Machine-readable <c>errorCode</c> from the error body, whatever the status — e.g.
    /// <c>"PARTNER_SUSPENDED"</c> / <c>"PARTNER_CONTRACT_REQUIRED"</c> on some 403s, or
    /// <see cref="LivenessErrorCodes.No3DEnrollment"/> on a 400. Null when the body carries none.
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
