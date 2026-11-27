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

    [Fact]
    public async Task ListSessionsAsync_SendsTheExactExternalUserIdFilterAndExposesRecommendationAndImages()
    {
        using var server = new FakeServer(200,
            "{\"data\":[{\"sessionId\":\"s1\",\"externalUserId\":\"cust 9981\",\"status\":\"IN_REVIEW\",\"outcome\":\"MANUAL\"," +
            "\"recommendation\":\"REVIEW\",\"createdAt\":\"2026-11-19T12:00:00.000Z\"," +
            "\"images\":{\"documentFront\":{\"url\":\"https://s3.example/front.jpg?sig=1\",\"expiresAt\":\"2026-11-19T13:00:00.000Z\"}," +
            "\"documentBack\":null,\"selfie\":null,\"documentFaceCrop\":null}}],\"total\":1,\"page\":1,\"limit\":20,\"totalPages\":1}");
        var protegey = new ProtegeyClient("key", server.BaseUrl);

        var result = await protegey.Kyc.ListSessionsAsync(new Dictionary<string, object?> { ["externalUserId"] = "cust 9981" });

        Assert.Equal("/partner-api/kyc/sessions?externalUserId=cust%209981", server.LastPathAndQuery);
        var row = Assert.IsType<Dictionary<string, object?>>(Assert.Single(Assert.IsType<List<object?>>(result["data"])));
        Assert.Equal("REVIEW", row["recommendation"]);
        var images = Assert.IsType<Dictionary<string, object?>>(row["images"]);
        var front = Assert.IsType<Dictionary<string, object?>>(images["documentFront"]);
        Assert.Equal("https://s3.example/front.jpg?sig=1", front["url"]);
        Assert.Equal("2026-11-19T13:00:00.000Z", front["expiresAt"]);
        Assert.Null(images["selfie"]);
    }

    [Fact]
    public async Task GetSessionAsync_ExposesRecommendationAndPresignedImages()
    {
        using var server = new FakeServer(200,
            "{\"sessionId\":\"s1\",\"status\":\"APPROVED\",\"outcome\":\"MANUAL\",\"recommendation\":\"REVIEW\"," +
            "\"images\":{\"documentFront\":null,\"documentBack\":null,\"selfie\":{\"url\":\"https://s3.example/selfie.jpg\",\"expiresAt\":\"2026-11-19T13:00:00.000Z\"},\"documentFaceCrop\":null}}");
        var protegey = new ProtegeyClient("key", server.BaseUrl);

        var result = await protegey.Kyc.GetSessionAsync("s1");

        Assert.Equal("REVIEW", result["recommendation"]);
        var images = Assert.IsType<Dictionary<string, object?>>(result["images"]);
        Assert.Equal("https://s3.example/selfie.jpg", Assert.IsType<Dictionary<string, object?>>(images["selfie"])["url"]);
    }

    [Fact]
    public async Task GetSessionAsync_ExposesGroupPresenceAndNetworkFlagGroupEntities()
    {
        using var server = new FakeServer(200,
            "{\"sessionId\":\"s1\",\"status\":\"APPROVED\",\"groupPresence\":[{\"entity\":\"Sample Pay Senegal\",\"status\":\"APPROVED\"," +
            "\"createdAt\":\"2026-03-02T10:00:00.000Z\",\"matchedOn\":[\"FACE\",\"GOVERNMENT_ID\"]}]," +
            "\"decision\":{\"identityResolution\":{\"networkFlag\":{\"flagged\":true,\"matchCount\":1,\"groupEntities\":[\"Sample Pay Senegal\"]}}}}");
        var protegey = new ProtegeyClient("key", server.BaseUrl);

        var result = await protegey.Kyc.GetSessionAsync("s1");

        var presence = Assert.IsType<Dictionary<string, object?>>(Assert.Single(Assert.IsType<List<object?>>(result["groupPresence"])));
        Assert.Equal("Sample Pay Senegal", presence["entity"]);
        Assert.Equal("APPROVED", presence["status"]);
        Assert.Equal(new object?[] { "FACE", "GOVERNMENT_ID" }, Assert.IsType<List<object?>>(presence["matchedOn"]));
        var decision = Assert.IsType<Dictionary<string, object?>>(result["decision"]);
        var identityResolution = Assert.IsType<Dictionary<string, object?>>(decision["identityResolution"]);
        var networkFlag = Assert.IsType<Dictionary<string, object?>>(identityResolution["networkFlag"]);
        Assert.Equal(new object?[] { "Sample Pay Senegal" }, Assert.IsType<List<object?>>(networkFlag["groupEntities"]));
    }

    [Fact]
    public async Task GetSessionAsync_GroupPresenceIsNullOutsideAGroup()
    {
        using var server = new FakeServer(200, "{\"sessionId\":\"s1\",\"status\":\"APPROVED\",\"groupPresence\":null}");
        var protegey = new ProtegeyClient("key", server.BaseUrl);

        var result = await protegey.Kyc.GetSessionAsync("s1");

        Assert.True(result.ContainsKey("groupPresence"));
        Assert.Null(result["groupPresence"]);
    }

    [Fact]
    public async Task UpdateSessionAsync_SendsStatusAndReviewNoteAndReturnsTheManualReview()
    {
        using var server = new FakeServer(200,
            "{\"sessionId\":\"s1\",\"status\":\"DECLINED\",\"outcome\":\"MANUAL\",\"recommendation\":\"REVIEW\"," +
            "\"decision\":{\"manualReview\":{\"reviewedBy\":\"Partner API\",\"previousStatus\":\"IN_REVIEW\",\"newStatus\":\"DECLINED\",\"note\":\"Expired ID\"}}}");
        var protegey = new ProtegeyClient("key", server.BaseUrl);

        var result = await protegey.Kyc.UpdateSessionAsync("s1", new Dictionary<string, object?> { ["status"] = "DECLINED", ["reviewNote"] = "Expired ID" });

        Assert.Equal("{\"status\":\"DECLINED\",\"reviewNote\":\"Expired ID\"}", server.LastRequestBody);
        Assert.Equal("REVIEW", result["recommendation"]);
        var decision = Assert.IsType<Dictionary<string, object?>>(result["decision"]);
        var review = Assert.IsType<Dictionary<string, object?>>(decision["manualReview"]);
        Assert.Equal("IN_REVIEW", review["previousStatus"]);
        Assert.Equal("DECLINED", review["newStatus"]);
    }
}
