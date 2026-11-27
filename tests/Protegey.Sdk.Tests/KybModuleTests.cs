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
    public async Task ScreenApplicantAsync_ExposesNetworkFlagGroupEntities()
    {
        using var server = new FakeServer(200,
            "{\"business\":{\"decision\":\"CLEAR\"},\"owners\":[],\"networkFlags\":[{\"label\":\"Business phone\",\"flagged\":true," +
            "\"matchCount\":1,\"flagConfidence\":40,\"groupEntities\":[\"Sample Pay Senegal\"]}]}");
        var protegey = new ProtegeyClient("key", server.BaseUrl);

        var result = await protegey.Kyb.ScreenApplicantAsync("c1");

        var flag = (Dictionary<string, object?>)((List<object?>)result["networkFlags"]!)[0]!;
        Assert.Equal(new object?[] { "Sample Pay Senegal" }, (List<object?>)flag["groupEntities"]!);
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

    [Fact]
    public async Task RequestKycVerificationAsync_SendsOnlyTheGivenFields()
    {
        using var server = new FakeServer(201,
            "{\"sessionId\":\"k1\",\"kybClientId\":\"c1\",\"url\":\"https://capture/x\",\"expiresAt\":\"2026-12-03T09:00:00.000Z\",\"emailedTo\":null}");
        var protegey = new ProtegeyClient("key", server.BaseUrl);

        var result = await protegey.Kyb.RequestKycVerificationAsync("c1", fullName: "Jane Sample", externalUserId: "director_17");

        Assert.Equal("POST", server.LastMethod);
        Assert.Equal("/partner-api/kyb/applicants/c1/kyc-sessions", server.LastPathAndQuery);
        Assert.Equal("{\"fullName\":\"Jane Sample\",\"externalUserId\":\"director_17\"}", server.LastRequestBody);
        Assert.Equal("c1", result["kybClientId"]);
        Assert.Equal("2026-12-03T09:00:00.000Z", result["expiresAt"]);
        Assert.Null(result["emailedTo"]);
    }

    [Fact]
    public async Task RequestKycVerificationAsync_SendsAnEmptyBodyByDefault()
    {
        using var server = new FakeServer(201, "{\"sessionId\":\"k1\"}");
        var protegey = new ProtegeyClient("key", server.BaseUrl);

        await protegey.Kyb.RequestKycVerificationAsync("c1");

        Assert.Equal("{}", server.LastRequestBody);
    }

    [Fact]
    public async Task ListKycVerificationsAsync_IsAGet()
    {
        using var server = new FakeServer(200, "{\"data\":[{\"sessionId\":\"k1\",\"status\":\"APPROVED\",\"nameMatch\":\"MATCH\"}]}");
        var protegey = new ProtegeyClient("key", server.BaseUrl);

        var result = await protegey.Kyb.ListKycVerificationsAsync("c1");

        Assert.Equal("GET", server.LastMethod);
        Assert.Equal("/partner-api/kyb/applicants/c1/kyc-sessions", server.LastPathAndQuery);
        var row = (Dictionary<string, object?>)((List<object?>)result["data"]!)[0]!;
        Assert.Equal("MATCH", row["nameMatch"]);
    }

    [Fact]
    public async Task ImportApplicantsAsync_PostsTheApplicantsAndReturnsTheReport()
    {
        using var server = new FakeServer(200,
            "{\"totalRows\":2,\"importedCount\":1,\"rejectedCount\":1,\"errors\":[{\"row\":1,\"field\":null,\"code\":\"contact_required\",\"error\":\"contactEmail or contactPhone is required\"}],\"importId\":\"i1\"}");
        var protegey = new ProtegeyClient("key", server.BaseUrl);

        var result = await protegey.Kyb.ImportApplicantsAsync(new[]
        {
            new Dictionary<string, object?> { ["legalName"] = "Sample SARL", ["country"] = "CI", ["contactName"] = "Awa", ["contactEmail"] = "awa@sample.ci" },
            new Dictionary<string, object?> { ["legalName"] = "Other SA", ["country"] = "SN", ["contactName"] = "Moussa" },
        });

        Assert.Equal("POST", server.LastMethod);
        Assert.Equal("/partner-api/kyb/applicants/import", server.LastPathAndQuery);
        using var sent = System.Text.Json.JsonDocument.Parse(server.LastRequestBody!);
        Assert.Equal(new[] { "applicants" }, sent.RootElement.EnumerateObject().Select(p => p.Name).ToArray());
        Assert.Equal(2, sent.RootElement.GetProperty("applicants").GetArrayLength());
        Assert.Equal("i1", result["importId"]);
        var error = (Dictionary<string, object?>)((List<object?>)result["errors"]!)[0]!;
        Assert.Equal("contact_required", error["code"]);
        Assert.Null(error["field"]);
    }

    [Fact]
    public async Task ImportApplicantsAsync_SendsDryRunWhenAsked()
    {
        using var server = new FakeServer(200, "{\"totalRows\":1,\"importedCount\":1,\"rejectedCount\":0,\"errors\":[],\"importId\":null}");
        var protegey = new ProtegeyClient("key", server.BaseUrl);

        var result = await protegey.Kyb.ImportApplicantsAsync(new[] { new Dictionary<string, object?> { ["legalName"] = "Sample SARL" } }, dryRun: true);

        Assert.Equal("{\"applicants\":[{\"legalName\":\"Sample SARL\"}],\"dryRun\":true}", server.LastRequestBody);
        Assert.Null(result["importId"]);
    }

    [Fact]
    public async Task GetImportAsync_IsAGet()
    {
        using var server = new FakeServer(200, "{\"importId\":\"i1\",\"status\":\"SCREENING\",\"flaggedClientIds\":[\"c1\"],\"completedAt\":null}");
        var protegey = new ProtegeyClient("key", server.BaseUrl);

        var result = await protegey.Kyb.GetImportAsync("i1");

        Assert.Equal("GET", server.LastMethod);
        Assert.Equal("/partner-api/kyb/imports/i1", server.LastPathAndQuery);
        Assert.Equal("SCREENING", result["status"]);
        Assert.Equal("c1", ((List<object?>)result["flaggedClientIds"]!)[0]);
    }
}
