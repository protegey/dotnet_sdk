using Xunit;

namespace Protegey.Sdk.Tests;

public class TransactionsModuleTests
{
    [Fact]
    public async Task ReportAsync_ReturnsTheDecision()
    {
        using var server = new FakeServer(201,
            "{\"transactionId\":\"tx-1\",\"decision\":\"CLEAR\",\"riskScore\":0,\"alerts\":[],\"deviceAction\":null}");
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
        Assert.Equal("CLEAR", result["decision"]);
    }

    [Fact]
    public async Task ReportAsync_ForwardsChannelCounterpartyInstitutionCodeAndCounterpartyCountry()
    {
        using var server = new FakeServer(201,
            "{\"transactionId\":\"tx-1\",\"decision\":\"CLEAR\",\"riskScore\":0,\"alerts\":[],\"deviceAction\":null}");
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
    public async Task ReportAsync_ForwardsLocationAndReturnsTheGpsSignals()
    {
        using var server = new FakeServer(201,
            "{\"transactionId\":\"tx-1\",\"decision\":\"REVIEW\",\"riskScore\":40,\"alerts\":[],\"deviceAction\":null," +
            "\"location\":{\"latitude\":14.693,\"longitude\":-17.447,\"accuracyMeters\":25,\"isMocked\":false,\"gpsIpDistanceKm\":1200,\"impliedTravelSpeedKmh\":null,\"isNewGpsArea\":true}}");
        var protegey = new ProtegeyClient("key", server.BaseUrl);

        var result = await protegey.Transactions.ReportAsync(new Dictionary<string, object?>
        {
            ["externalTransactionId"] = "tx-1",
            ["externalCustomerId"] = "cust-1",
            ["direction"] = "DEBIT",
            ["amount"] = 5000,
            ["transactionType"] = "cashout",
            ["location"] = new Dictionary<string, object?> { ["latitude"] = 14.6928, ["longitude"] = -17.4467, ["accuracyMeters"] = 25 },
        });

        Assert.Contains("\"location\":{\"latitude\":14.6928,\"longitude\":-17.4467,\"accuracyMeters\":25}", server.LastRequestBody);
        var location = (Dictionary<string, object?>)result["location"]!;
        Assert.Equal(false, location["isMocked"]);
        Assert.Equal(true, location["isNewGpsArea"]);
        Assert.Null(location["impliedTravelSpeedKmh"]);
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

    [Fact]
    public async Task ReportAsync_PostsToTransactionsAndDefaultsOccurredAt()
    {
        using var server = new FakeServer(201, "{\"transactionId\":\"t\",\"decision\":\"REVIEW\",\"riskScore\":20,\"deviceAction\":\"ALLOW\",\"alerts\":[]}");
        var protegey = new ProtegeyClient("key", server.BaseUrl);

        var result = await protegey.Transactions.ReportAsync(new Dictionary<string, object?> { ["externalTransactionId"] = "tx-1" });

        Assert.Equal("POST", server.LastMethod);
        Assert.Equal("/partner-api/transactions", server.LastPathAndQuery);
        Assert.Contains("\"occurredAt\":", server.LastRequestBody);
        Assert.Equal("ALLOW", result["deviceAction"]);
    }

    [Fact]
    public async Task ListAsync_SendsFiltersAsQueryString()
    {
        using var server = new FakeServer(200, "{\"data\":[{\"amount\":\"15000.00\"}],\"total\":1,\"page\":1,\"limit\":20,\"totalPages\":1}");
        var protegey = new ProtegeyClient("key", server.BaseUrl);

        var result = await protegey.Transactions.ListAsync(new Dictionary<string, object?>
        {
            ["decision"] = "BLOCKED",
            ["dateFrom"] = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc),
            ["limit"] = 50,
            ["externalCustomerId"] = null,
        });

        Assert.Equal("/partner-api/transactions?decision=BLOCKED&dateFrom=2026-11-01T00%3A00%3A00.0000000Z&limit=50", server.LastPathAndQuery);
        var row = (Dictionary<string, object?>)((List<object?>)result["data"]!)[0]!;
        Assert.Equal("15000.00", row["amount"]);
    }

    [Fact]
    public async Task GetAsync_AcceptsAnExternalTransactionId()
    {
        using var server = new FakeServer(200, "{\"transactionId\":\"uuid\",\"externalTransactionId\":\"tx/1\",\"alerts\":[]}");
        var protegey = new ProtegeyClient("key", server.BaseUrl);

        await protegey.Transactions.GetAsync("tx/1");

        Assert.Equal("/partner-api/transactions/tx%2F1", server.LastPathAndQuery);
    }

    [Fact]
    public async Task Errors_ExposeTheGuardErrorCode()
    {
        using var server = new FakeServer(403, "{\"errorCode\":\"PARTNER_SUSPENDED\",\"message\":\"Account suspended\"}");
        var protegey = new ProtegeyClient("key", server.BaseUrl);

        var ex = await Assert.ThrowsAsync<ProtegeyApiException>(() => protegey.Transactions.ReportAsync(new Dictionary<string, object?>()));

        Assert.Equal(403, ex.Status);
        Assert.Equal("PARTNER_SUSPENDED", ex.ErrorCode);
        Assert.Equal("Account suspended", ex.Message);
    }

    [Fact]
    public async Task Errors_JoinValidationMessageArrays()
    {
        using var server = new FakeServer(400, "{\"statusCode\":400,\"message\":[\"amount must be positive\",\"direction is invalid\"],\"error\":\"Bad Request\"}");
        var protegey = new ProtegeyClient("key", server.BaseUrl);

        var ex = await Assert.ThrowsAsync<ProtegeyApiException>(() => protegey.Transactions.ListAsync());

        Assert.Equal("amount must be positive, direction is invalid", ex.Message);
        Assert.Null(ex.ErrorCode);
    }
}
