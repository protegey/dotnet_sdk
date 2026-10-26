namespace Protegey.Sdk;

public class ProtegeyApiException : Exception
{
    public int Status { get; }

    public ProtegeyApiException(int status, string message) : base(message)
    {
        Status = status;
    }
}
