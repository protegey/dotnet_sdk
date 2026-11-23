using Xunit;

namespace Protegey.Sdk.Tests;

public class LivenessModuleTests
{
    private const string Started =
        "{\"sessionId\":\"lv_1\",\"mode\":\"LIVENESS\",\"status\":\"PENDING\",\"externalUserId\":\"cust-1\"," +
        "\"url\":\"https://kyc.protegey.com/liveness?token=t\",\"captureUrl\":\"https://kyc.protegey.com/liveness?token=t\",\"expiresInMinutes\":30}";

    private const string Session =
        "{\"id\":\"uuid-1\",\"sessionId\":\"lv_1\",\"mode\":\"REVERIFICATION\",\"status\":\"COMPLETED\",\"externalUserId\":\"cust-1\"," +
        "\"referenceEnrollmentId\":\"enr-9\",\"livenessProven\":true,\"matchLevel\":15,\"matched\":true,\"duplicateFaceEnrollmentIds\":null," +
        "\"attempts\":1,\"isSandbox\":true,\"completedAt\":\"2026-11-23T10:01:00.000Z\",\"createdAt\":\"2026-11-23T10:00:00.000Z\"," +
        "\"billableChecks\":1,\"amount\":\"0.10\",\"currency\":\"USD\"," +
        "\"checks\":[{\"id\":\"chk-1\",\"livenessProven\":true,\"matchLevel\":15,\"billable\":true,\"unitPrice\":\"0.10\",\"currency\":\"USD\",\"createdAt\":\"2026-11-23T10:01:00.000Z\"}]}";

    [Fact]
    public async Task StartSessionAsync_PostsLivenessModeAndReturnsTheHostedUrl()
    {
        using var server = new FakeServer(201, Started);
        var protegey = new ProtegeyClient("key", server.BaseUrl);

        var result = await protegey.Liveness.StartSessionAsync("cust-1");

        Assert.Equal("POST", server.LastMethod);
        Assert.Equal("/partner-api/liveness/sessions", server.LastPathAndQuery);
        Assert.Equal("{\"mode\":\"LIVENESS\",\"externalUserId\":\"cust-1\"}", server.LastRequestBody);
        Assert.Equal("lv_1", result["sessionId"]);
        Assert.Equal("https://kyc.protegey.com/liveness?token=t", result["url"]);
        Assert.Equal(30.0, result["expiresInMinutes"]);
    }

    [Fact]
    public async Task StartSessionAsync_OmitsExternalUserIdWhenNull()
    {
        using var server = new FakeServer(201, Started);
        var protegey = new ProtegeyClient("key", server.BaseUrl);

        await protegey.Liveness.StartSessionAsync();

        Assert.Equal("{\"mode\":\"LIVENESS\"}", server.LastRequestBody);
    }

    [Fact]
    public async Task StartReverificationAsync_PostsReverificationModeWithOptionalReference()
    {
        using var server = new FakeServer(201, Started);
        var protegey = new ProtegeyClient("key", server.BaseUrl);

        await protegey.Liveness.StartReverificationAsync("cust-1");
        Assert.Equal("{\"mode\":\"REVERIFICATION\",\"externalUserId\":\"cust-1\"}", server.LastRequestBody);

        await protegey.Liveness.StartReverificationAsync("cust-1", referenceVerificationId: "kyc-7");
        Assert.Equal("{\"mode\":\"REVERIFICATION\",\"externalUserId\":\"cust-1\",\"referenceVerificationId\":\"kyc-7\"}", server.LastRequestBody);
        Assert.Equal("/partner-api/liveness/sessions", server.LastPathAndQuery);
    }

    [Fact]
    public async Task StartReverificationAsync_ExposesNo3DEnrollment()
    {
        using var server = new FakeServer(400, "{\"errorCode\":\"NO_3D_ENROLLMENT\",\"message\":\"No 3D record (KYC) exists for this user — run a KYC session first\"}");
        var protegey = new ProtegeyClient("key", server.BaseUrl);

        var ex = await Assert.ThrowsAsync<ProtegeyApiException>(() => protegey.Liveness.StartReverificationAsync("cust-unknown"));

        Assert.Equal(400, ex.Status);
        Assert.Equal(LivenessErrorCodes.No3DEnrollment, ex.ErrorCode);
        Assert.Equal("NO_3D_ENROLLMENT", LivenessErrorCodes.No3DEnrollment);
        Assert.StartsWith("No 3D record (KYC)", ex.Message);
    }

    [Fact]
    public async Task GetSessionAsync_ParsesTheResultAndBilling()
    {
        using var server = new FakeServer(200, Session);
        var protegey = new ProtegeyClient("secret-key", server.BaseUrl);

        var result = await protegey.Liveness.GetSessionAsync("lv 1");

        Assert.Equal("GET", server.LastMethod);
        Assert.Equal("/partner-api/liveness/sessions/lv%201", server.LastPathAndQuery);
        Assert.Equal("secret-key", server.LastApiKey);
        Assert.Equal("COMPLETED", result["status"]);
        Assert.Equal(true, result["livenessProven"]);
        Assert.Equal(15.0, result["matchLevel"]);
        Assert.Equal(true, result["matched"]);
        Assert.Null(result["duplicateFaceEnrollmentIds"]);
        Assert.Equal(1.0, result["billableChecks"]);
        Assert.Equal("0.10", result["amount"]);
        Assert.Equal("USD", result["currency"]);
        var check = Assert.IsType<Dictionary<string, object?>>(Assert.Single(Assert.IsType<List<object?>>(result["checks"])));
        Assert.Equal(true, check["billable"]);
        Assert.Equal("0.10", check["unitPrice"]);
    }

    [Fact]
    public async Task ListSessionsAsync_SendsTheFiltersAndReturnsThePage()
    {
        using var server = new FakeServer(200, "{\"data\":[" + Session + "],\"total\":1,\"page\":2,\"limit\":10,\"totalPages\":1}");
        var protegey = new ProtegeyClient("key", server.BaseUrl);

        var result = await protegey.Liveness.ListSessionsAsync(new Dictionary<string, object?>
        {
            ["page"] = 2,
            ["limit"] = 10,
            ["externalUserId"] = "cust-1",
            ["mode"] = "REVERIFICATION",
        });

        Assert.Equal("GET", server.LastMethod);
        Assert.Equal("/partner-api/liveness/sessions?page=2&limit=10&externalUserId=cust-1&mode=REVERIFICATION", server.LastPathAndQuery);
        var row = Assert.IsType<Dictionary<string, object?>>(Assert.Single(Assert.IsType<List<object?>>(result["data"])));
        Assert.Equal("REVERIFICATION", row["mode"]);
        Assert.Equal(1.0, result["total"]);
    }
}
