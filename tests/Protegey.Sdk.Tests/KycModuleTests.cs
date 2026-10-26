using Xunit;

namespace Protegey.Sdk.Tests;

public class KycModuleTests
{
    private static FakeServer StartServer() => new(async (context, method) =>
    {
        string response = method == "POST"
            ? "{\"sessionId\":\"sess_abc\",\"url\":\"https://verify.didit.me/session/abc\"}"
            : "{\"sessionId\":\"sess_1\",\"status\":\"Approved\"}";
        context.Response.StatusCode = 200;
        context.Response.ContentType = "application/json";
        var bytes = System.Text.Encoding.UTF8.GetBytes(response);
        await context.Response.OutputStream.WriteAsync(bytes);
    });

    [Fact]
    public async Task StartSessionAsync_ReturnsTheHostedUrl()
    {
        using var server = StartServer();
        var protegey = new ProtegeyClient("test-api-key", server.BaseUrl);

        var result = await protegey.Kyc.StartSessionAsync("cust-1");

        Assert.Equal("sess_abc", result["sessionId"]);
        Assert.Equal("https://verify.didit.me/session/abc", result["url"]);
    }

    [Fact]
    public async Task GetSessionAsync_ReturnsTheCurrentStatus()
    {
        using var server = StartServer();
        var protegey = new ProtegeyClient("test-api-key", server.BaseUrl);

        var result = await protegey.Kyc.GetSessionAsync("sess/1");

        Assert.Equal("Approved", result["status"]);
    }
}
