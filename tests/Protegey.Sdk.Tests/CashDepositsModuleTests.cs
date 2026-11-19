using Xunit;

namespace Protegey.Sdk.Tests;

public class CashDepositsModuleTests
{
    private const string DepositJson =
        "{\"cashDepositId\":\"cd-1\",\"externalTransactionId\":\"dep-0001\",\"externalCustomerId\":\"cust-9981\"," +
        "\"amount\":\"2500000.00\",\"currency\":\"XOF\",\"channel\":\"BRANCH\",\"decision\":\"REVIEW\",\"riskScore\":20," +
        "\"depositedAt\":\"2026-11-18T09:30:00.000Z\"}";

    [Fact]
    public async Task RecordAsync_PostsTheDepositAndReturnsTheDecision()
    {
        using var server = new FakeServer(201,
            "{\"cashDepositId\":\"cd-1\",\"externalTransactionId\":\"dep-0001\",\"decision\":\"REVIEW\",\"riskScore\":20," +
            "\"alerts\":[{\"id\":\"a1\",\"alertNumber\":1043,\"ruleCode\":\"IND006-KYC1\",\"status\":\"OPEN\",\"decisionVerdict\":\"REVIEW\"}]}");
        var protegey = new ProtegeyClient("key", server.BaseUrl);

        var result = await protegey.CashDeposits.RecordAsync(new Dictionary<string, object?>
        {
            ["externalTransactionId"] = "dep-0001",
            ["externalCustomerId"] = "cust-9981",
            ["amount"] = 2500000,
            ["currency"] = "XOF",
            ["channel"] = "BRANCH",
            ["branchId"] = "DKR-PLATEAU",
            ["metadata"] = new Dictionary<string, object?> { ["tellerId"] = "T-04" },
        });

        Assert.Equal("POST", server.LastMethod);
        Assert.Equal("/partner-api/cash-deposits", server.LastPathAndQuery);
        Assert.Contains("\"amount\":2500000", server.LastRequestBody);
        Assert.Contains("\"metadata\":{\"tellerId\":\"T-04\"}", server.LastRequestBody);
        Assert.Equal("cd-1", result["cashDepositId"]);
        Assert.Equal("REVIEW", result["decision"]);
        var alert = Assert.IsType<Dictionary<string, object?>>(Assert.Single(Assert.IsType<List<object?>>(result["alerts"])));
        Assert.Equal("IND006-KYC1", alert["ruleCode"]);
    }

    [Fact]
    public async Task RecordAsync_ThrowsA409OnADuplicateExternalTransactionId()
    {
        using var server = new FakeServer(409, "{\"statusCode\":409,\"message\":\"Transaction already submitted\"}");
        var protegey = new ProtegeyClient("key", server.BaseUrl);

        var ex = await Assert.ThrowsAsync<ProtegeyApiException>(() => protegey.CashDeposits.RecordAsync(new Dictionary<string, object?>
        {
            ["externalTransactionId"] = "dep-0001",
            ["externalCustomerId"] = "cust-9981",
            ["amount"] = 1000,
            ["currency"] = "XOF",
        }));

        Assert.Equal(409, ex.Status);
        Assert.Equal("Transaction already submitted", ex.Message);
    }

    [Fact]
    public async Task ListAsync_SendsEveryFilterAsAQueryParameter()
    {
        using var server = new FakeServer(200, "{\"data\":[" + DepositJson + "],\"total\":1,\"page\":1,\"limit\":100,\"totalPages\":1}");
        var protegey = new ProtegeyClient("key", server.BaseUrl);

        var result = await protegey.CashDeposits.ListAsync(new Dictionary<string, object?>
        {
            ["page"] = 1,
            ["limit"] = 100,
            ["dateFrom"] = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc),
            ["dateTo"] = "2026-11-30T23:59:59Z",
            ["externalCustomerId"] = "cust-9981",
            ["minAmount"] = 1000000,
            ["maxAmount"] = 5000000.5,
            ["currency"] = "XOF",
            ["channel"] = "BRANCH",
            ["decision"] = "REVIEW",
            ["branchId"] = "DKR-PLATEAU",
            ["agentId"] = "AG-7",
        });

        Assert.Equal("GET", server.LastMethod);
        Assert.Equal(
            "/partner-api/cash-deposits?page=1&limit=100&dateFrom=2026-11-01T00%3A00%3A00.0000000Z&dateTo=2026-11-30T23%3A59%3A59Z" +
            "&externalCustomerId=cust-9981&minAmount=1000000&maxAmount=5000000.5&currency=XOF&channel=BRANCH&decision=REVIEW" +
            "&branchId=DKR-PLATEAU&agentId=AG-7",
            server.LastPathAndQuery);
        var row = Assert.IsType<Dictionary<string, object?>>(Assert.Single(Assert.IsType<List<object?>>(result["data"])));
        // amount is a decimal string on the wire, not a number
        Assert.Equal("2500000.00", row["amount"]);
        Assert.Equal(1.0, result["totalPages"]);
    }

    [Fact]
    public async Task GetAsync_AcceptsTheExternalTransactionIdAndEscapesIt()
    {
        using var server = new FakeServer(200, DepositJson.TrimEnd('}') + ",\"alerts\":[]}");
        var protegey = new ProtegeyClient("key", server.BaseUrl);

        var result = await protegey.CashDeposits.GetAsync("dep 0001/a");

        Assert.Equal("/partner-api/cash-deposits/dep%200001%2Fa", server.LastPathAndQuery);
        Assert.Equal("dep-0001", result["externalTransactionId"]);
        Assert.Empty(Assert.IsType<List<object?>>(result["alerts"]));
    }
}
