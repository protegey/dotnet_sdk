using Xunit;

namespace Protegey.Sdk.Tests;

public class AlertsModuleTests
{
    private const string Alert =
        "{\"id\":\"a1\",\"alertNumber\":1032,\"ruleCode\":\"VEL-001\",\"status\":\"OPEN\",\"decisionVerdict\":\"ALERT\"," +
        "\"disposition\":null,\"ruleSeverity\":\"review\",\"transaction\":{\"amount\":\"15000.00\",\"currency\":\"XOF\",\"direction\":\"DEBIT\"}}";

    [Fact]
    public async Task ListAsync_LowercasesStatusAndDispositionFilters()
    {
        using var server = new FakeServer(200, "{\"data\":[" + Alert + "],\"total\":1,\"page\":1,\"limit\":20,\"totalPages\":1}");
        var protegey = new ProtegeyClient("key", server.BaseUrl);

        var result = await protegey.Alerts.ListAsync(new Dictionary<string, object?>
        {
            ["status"] = "MORE_INFO_REQUESTED",
            ["disposition"] = "FALSE_POSITIVE",
            ["alertNumber"] = 1032,
        });

        Assert.Equal("GET", server.LastMethod);
        Assert.Equal("/partner-api/alerts?status=more_info_requested&disposition=false_positive&alertNumber=1032", server.LastPathAndQuery);
        Assert.Single((List<object?>)result["data"]!);
    }

    [Fact]
    public async Task GetAsync_ReturnsTheAlertWithContext()
    {
        using var server = new FakeServer(200, Alert);
        var protegey = new ProtegeyClient("key", server.BaseUrl);

        var result = await protegey.Alerts.GetAsync("a1");

        Assert.Equal("/partner-api/alerts/a1", server.LastPathAndQuery);
        Assert.Equal("ALERT", result["decisionVerdict"]);
        Assert.Equal("review", result["ruleSeverity"]);
        Assert.Equal("15000.00", ((Dictionary<string, object?>)result["transaction"]!)["amount"]);
    }

    [Fact]
    public async Task UpdateAsync_PatchesAndSendsExplicitNulls()
    {
        using var server = new FakeServer(200, Alert);
        var protegey = new ProtegeyClient("key", server.BaseUrl);

        await protegey.Alerts.UpdateAsync("a1", new Dictionary<string, object?>
        {
            ["status"] = "CONFIRMED",
            ["disposition"] = "CONFIRMED_FRAUD",
            ["assignedToUserId"] = null,
        });

        Assert.Equal("PATCH", server.LastMethod);
        Assert.Equal("/partner-api/alerts/a1", server.LastPathAndQuery);
        Assert.Contains("\"assignedToUserId\":null", server.LastRequestBody);
        Assert.Contains("\"disposition\":\"CONFIRMED_FRAUD\"", server.LastRequestBody);
    }

    [Fact]
    public async Task ConvertToCaseAsync_ReturnsAlertAndCase()
    {
        using var server = new FakeServer(201,
            "{\"alert\":" + Alert + ",\"case\":{\"id\":\"c1\",\"caseNumber\":48,\"status\":\"OPEN\",\"priority\":\"medium\",\"linkedAlertIds\":[\"a1\"]}}");
        var protegey = new ProtegeyClient("key", server.BaseUrl);

        var result = await protegey.Alerts.ConvertToCaseAsync("a1");

        Assert.Equal("POST", server.LastMethod);
        Assert.Equal("/partner-api/alerts/a1/convert-to-case", server.LastPathAndQuery);
        Assert.Equal(48.0, ((Dictionary<string, object?>)result["case"]!)["caseNumber"]);
    }

    [Fact]
    public async Task FlagTransactionAsync_PostsTheTransactionId()
    {
        using var server = new FakeServer(201, Alert);
        var protegey = new ProtegeyClient("key", server.BaseUrl);

        await protegey.Alerts.FlagTransactionAsync("tx-uuid");

        Assert.Equal("/partner-api/alerts/flag-transaction", server.LastPathAndQuery);
        Assert.Equal("{\"transactionId\":\"tx-uuid\"}", server.LastRequestBody);
    }
}
