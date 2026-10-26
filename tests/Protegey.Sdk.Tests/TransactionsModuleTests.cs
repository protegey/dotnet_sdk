using Xunit;

namespace Protegey.Sdk.Tests;

public class TransactionsModuleTests
{
    [Fact]
    public async Task ReportAsync_ReturnsTheDecision()
    {
        using var server = new FakeServer(201,
            "{\"transactionId\":\"tx-1\",\"decision\":\"clear\",\"riskScore\":0,\"alerts\":[],\"deviceAction\":null}");
        var protegey = new ProtegeyClient("key", server.BaseUrl);

        var result = await protegey.Transactions.ReportAsync(new Dictionary<string, object?>
        {
            ["externalTransactionId"] = "tx-1",
            ["externalCustomerId"] = "cust-1",
            ["direction"] = "DEBIT",
            ["amount"] = 5000,
            ["transactionType"] = "cashout",
        });

        Assert.Equal("tx-1", result["transactionId"]);
        Assert.Equal("clear", result["decision"]);
    }

    [Fact]
    public async Task ReportAsync_ForwardsChannelCounterpartyInstitutionCodeAndCounterpartyCountry()
    {
        using var server = new FakeServer(201,
            "{\"transactionId\":\"tx-1\",\"decision\":\"clear\",\"riskScore\":0,\"alerts\":[],\"deviceAction\":null}");
        var protegey = new ProtegeyClient("key", server.BaseUrl);

        await protegey.Transactions.ReportAsync(new Dictionary<string, object?>
        {
            ["externalTransactionId"] = "tx-1",
            ["externalCustomerId"] = "cust-1",
            ["direction"] = "DEBIT",
            ["amount"] = 2000000,
            ["transactionType"] = "wire",
            ["channel"] = "online",
            ["counterpartyInstitutionCode"] = "ECOBKTGTG",
            ["counterpartyCountry"] = "SN",
        });

        var body = server.LastRequestBody;
        Assert.Contains("\"channel\":\"online\"", body);
        Assert.Contains("\"counterpartyInstitutionCode\":\"ECOBKTGTG\"", body);
        Assert.Contains("\"counterpartyCountry\":\"SN\"", body);
    }

    [Fact]
    public async Task ReportAsync_ThrowsOnANonSuccessResponse()
    {
        using var server = new FakeServer(422, "{\"message\":\"amount must be positive\"}");
        var protegey = new ProtegeyClient("key", server.BaseUrl);

        var ex = await Assert.ThrowsAsync<ProtegeyApiException>(() => protegey.Transactions.ReportAsync(new Dictionary<string, object?>
        {
            ["externalTransactionId"] = "tx-1",
            ["externalCustomerId"] = "cust-1",
            ["direction"] = "DEBIT",
            ["amount"] = -1,
            ["transactionType"] = "cashout",
        }));

        Assert.Equal(422, ex.Status);
        Assert.Equal("amount must be positive", ex.Message);
    }
}
