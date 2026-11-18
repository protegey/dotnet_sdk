using Xunit;

namespace Protegey.Sdk.Tests;

public class KycModuleTests
{
    private static FakeServer StartServer() => new(async (context, method) =>
    {
        string response = method == "POST"
            ? "{\"sessionId\":\"sess_abc\",\"url\":\"https://kyc.protegey.com/?enrollmentId=abc&token=t\"}"
            : "{\"sessionId\":\"sess_1\",\"status\":\"APPROVED\"}";
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
        Assert.Equal("https://kyc.protegey.com/?enrollmentId=abc&token=t", result["url"]);
    }

    [Fact]
    public async Task GetSessionAsync_ReturnsTheCurrentStatus()
    {
        using var server = StartServer();
        var protegey = new ProtegeyClient("test-api-key", server.BaseUrl);

        var result = await protegey.Kyc.GetSessionAsync("sess/1");

        Assert.Equal("APPROVED", result["status"]);
    }

    [Fact]
    public async Task ListSessionsAsync_SendsStatusInTheTitleCaseTheApiAccepts()
    {
        using var server = new FakeServer(200, "{\"data\":[],\"total\":0,\"page\":1,\"limit\":20,\"totalPages\":1}");
        var protegey = new ProtegeyClient("key", server.BaseUrl);

        var result = await protegey.Kyc.ListSessionsAsync(new Dictionary<string, object?> { ["status"] = "IN_REVIEW", ["page"] = 2 });

        Assert.Equal("GET", server.LastMethod);
        Assert.Equal("/partner-api/kyc/sessions?status=In%20Review&page=2", server.LastPathAndQuery);
        Assert.Equal(1.0, result["totalPages"]);
    }

    [Theory]
    [InlineData("KYC_EXPIRED", "Kyc Expired")]
    [InlineData("In Review", "In Review")]
    [InlineData("approved", "Approved")]
    public void ToTitleCase_MapsWireValuesToTheFilterForm(string input, string expected)
        => Assert.Equal(expected, KycModule.ToTitleCase(input));

    [Fact]
    public async Task UpdateSessionAsync_PatchesTheSession()
    {
        using var server = new FakeServer(200, "{\"sessionId\":\"s1\",\"status\":\"APPROVED\",\"outcome\":\"MANUAL\"}");
        var protegey = new ProtegeyClient("key", server.BaseUrl);

        var result = await protegey.Kyc.UpdateSessionAsync("s1", new Dictionary<string, object?> { ["status"] = "APPROVED", ["reviewNote"] = "ok" });

        Assert.Equal("PATCH", server.LastMethod);
        Assert.Equal("/partner-api/kyc/sessions/s1", server.LastPathAndQuery);
        Assert.Contains("\"status\":\"APPROVED\"", server.LastRequestBody);
        Assert.Equal("MANUAL", result["outcome"]);
    }

    [Fact]
    public async Task ResendSessionAsync_PostsToResend()
    {
        using var server = new FakeServer(201, "{\"sessionId\":\"s1\",\"url\":\"https://x\"}");
        var protegey = new ProtegeyClient("key", server.BaseUrl);

        var result = await protegey.Kyc.ResendSessionAsync("s1");

        Assert.Equal("POST", server.LastMethod);
        Assert.Equal("/partner-api/kyc/sessions/s1/resend", server.LastPathAndQuery);
        Assert.Equal("https://x", result["url"]);
    }

    [Fact]
    public async Task EveryRequestCarriesTheApiKeyHeader()
    {
        using var server = new FakeServer(200, "{}");
        var protegey = new ProtegeyClient("secret-key", server.BaseUrl);

        await protegey.Kyc.GetSessionAsync("a b");

        Assert.Equal("secret-key", server.LastApiKey);
        Assert.Equal("/partner-api/kyc/sessions/a%20b", server.LastPathAndQuery);
    }
}
