using Xunit;

namespace Protegey.Sdk.Tests;

public class KybModuleTests
{
    private const string Business =
        "{\"id\":\"c1\",\"contactName\":\"John\",\"contactEmail\":null,\"contactPhone\":\"+237690000000\",\"status\":\"INVITED\"";

    [Fact]
    public async Task InviteApplicantAsync_SendsOnlyTheGivenContactAndReturnsApplicationUrl()
    {
        using var server = new FakeServer(201, Business + ",\"applicationUrl\":\"https://portal/client-application?token=t\"}");
        var protegey = new ProtegeyClient("key", server.BaseUrl);

        var result = await protegey.Kyb.InviteApplicantAsync("John", contactPhone: "+237690000000");

        Assert.Equal("POST", server.LastMethod);
        Assert.Equal("/partner-api/kyb/applicants", server.LastPathAndQuery);
        using var sent = System.Text.Json.JsonDocument.Parse(server.LastRequestBody!);
        Assert.Equal(new[] { "contactName", "contactPhone" }, sent.RootElement.EnumerateObject().Select(p => p.Name).ToArray());
        Assert.Equal("+237690000000", sent.RootElement.GetProperty("contactPhone").GetString());
        Assert.Equal("https://portal/client-application?token=t", result["applicationUrl"]);
        Assert.Null(result["contactEmail"]);
    }

    [Fact]
    public async Task ListApplicantsAsync_StripsInternalSubmissionFields()
    {
        using var server = new FakeServer(200,
            "{\"data\":[" + Business + ",\"submission\":{\"id\":\"s1\",\"status\":\"PENDING\",\"tokenHash\":\"abc\",\"documentFilePath\":\"k/1.pdf\",\"documentFileName\":\"1.pdf\"}}]," +
            "\"total\":1,\"page\":1,\"limit\":20,\"totalPages\":1}");
        var protegey = new ProtegeyClient("key", server.BaseUrl);

        var result = await protegey.Kyb.ListApplicantsAsync(new Dictionary<string, object?> { ["stage"] = "responded" });

        Assert.Equal("/partner-api/kyb/applicants?stage=responded", server.LastPathAndQuery);
        var row = (Dictionary<string, object?>)((List<object?>)result["data"]!)[0]!;
        var submission = (Dictionary<string, object?>)row["submission"]!;
        Assert.False(submission.ContainsKey("tokenHash"));
        Assert.False(submission.ContainsKey("documentFilePath"));
        Assert.Equal("1.pdf", submission["documentFileName"]);
    }

    [Fact]
    public async Task GetApplicantAsync_StripsInternalSubmissionFields()
    {
        using var server = new FakeServer(200, Business + ",\"submission\":{\"id\":\"s1\",\"tokenHash\":\"abc\"}}");
        var protegey = new ProtegeyClient("key", server.BaseUrl);

        var result = await protegey.Kyb.GetApplicantAsync("c1");

        Assert.Equal("/partner-api/kyb/applicants/c1", server.LastPathAndQuery);
        Assert.False(((Dictionary<string, object?>)result["submission"]!).ContainsKey("tokenHash"));
    }

    [Fact]
    public async Task ResendInvitationAsync_ReturnsTheSubmissionWithApplicationUrl()
    {
        using var server = new FakeServer(201, "{\"id\":\"s1\",\"clientBusinessId\":\"c1\",\"status\":\"PENDING\",\"tokenHash\":\"new\",\"applicationUrl\":\"https://portal/x\"}");
        var protegey = new ProtegeyClient("key", server.BaseUrl);

        var result = await protegey.Kyb.ResendInvitationAsync("c1");

        Assert.Equal("POST", server.LastMethod);
        Assert.Equal("/partner-api/kyb/applicants/c1/resend-invitation", server.LastPathAndQuery);
        Assert.Equal("https://portal/x", result["applicationUrl"]);
        Assert.False(result.ContainsKey("tokenHash"));
    }

    [Fact]
    public async Task ScreenApplicantAsync_IsAGet()
    {
        using var server = new FakeServer(200, "{\"business\":{\"decision\":\"REVIEW\"},\"owners\":[],\"degraded\":false}");
        var protegey = new ProtegeyClient("key", server.BaseUrl);

        var result = await protegey.Kyb.ScreenApplicantAsync("c1");

        Assert.Equal("GET", server.LastMethod);
        Assert.Equal("/partner-api/kyb/applicants/c1/screen", server.LastPathAndQuery);
        Assert.Equal("REVIEW", ((Dictionary<string, object?>)result["business"]!)["decision"]);
    }

    [Fact]
    public async Task DecideAsync_PatchesTheDecision()
    {
        using var server = new FakeServer(200, Business.Replace("INVITED", "MORE_INFO_REQUIRED") + ",\"applicationUrl\":\"https://portal/y\"}");
        var protegey = new ProtegeyClient("key", server.BaseUrl);

        var result = await protegey.Kyb.DecideAsync("c1", "REQUEST_MORE_INFO", note: "Upload your licence");

        Assert.Equal("PATCH", server.LastMethod);
        Assert.Equal("/partner-api/kyb/applicants/c1/decide", server.LastPathAndQuery);
        Assert.Equal("{\"decision\":\"REQUEST_MORE_INFO\",\"note\":\"Upload your licence\"}", server.LastRequestBody);
        Assert.Equal("https://portal/y", result["applicationUrl"]);
    }
}
