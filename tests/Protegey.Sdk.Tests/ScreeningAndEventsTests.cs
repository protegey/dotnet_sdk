using Xunit;

namespace Protegey.Sdk.Tests;

public class ScreeningAndEventsTests
{
    [Fact]
    public async Task DeviceEvents_RecordAsync_PostsTheEvent()
    {
        using var server = new FakeServer(201, "{\"recorded\":true,\"action\":\"SOFT_CHALLENGE\",\"riskScore\":15}");
        var protegey = new ProtegeyClient("key", server.BaseUrl);

        var result = await protegey.DeviceEvents.RecordAsync(new Dictionary<string, object?>
        {
            ["eventId"] = "evt-1",
            ["visitorId"] = "v-1",
            ["deviceAttributes"] = new Dictionary<string, object?> { ["platform"] = "android", ["isVpnActive"] = true },
        });

        Assert.Equal("/partner-api/device-events", server.LastPathAndQuery);
        Assert.Contains("\"deviceAttributes\":{\"platform\":\"android\",\"isVpnActive\":true}", server.LastRequestBody);
        Assert.Equal("SOFT_CHALLENGE", result["action"]);
    }

    [Fact]
    public async Task BehavioralEvents_RecordAsync_PostsTheEvent()
    {
        using var server = new FakeServer(201,
            "{\"status\":\"SCORED\",\"deviationScore\":70,\"confidenceTier\":\"HIGH\",\"stepUpRecommended\":true,\"escalatedAlertId\":null}");
        var protegey = new ProtegeyClient("key", server.BaseUrl);

        var result = await protegey.BehavioralEvents.RecordAsync(new Dictionary<string, object?>
        {
            ["externalCustomerId"] = "cust-1",
            ["sessionId"] = "sess-1",
            ["session"] = new Dictionary<string, object?> { ["loginHourBucket"] = 3 },
        });

        Assert.Equal("/partner-api/behavioral-events", server.LastPathAndQuery);
        Assert.Equal("SCORED", result["status"]);
        Assert.Equal(true, result["stepUpRecommended"]);
        Assert.Null(result["escalatedAlertId"]);
    }

    [Fact]
    public async Task Sanctions_ScreenAsync_LowercasesTheType()
    {
        using var server = new FakeServer(201,
            "{\"decision\":\"REVIEW\",\"score\":0.71,\"pepMatch\":false,\"matches\":[{\"source\":\"dfat\",\"matchedOn\":\"name\",\"isPep\":false}]," +
            "\"provider\":\"protegey\",\"providers\":[\"protegey\"],\"defaultListChecked\":true,\"degraded\":false}");
        var protegey = new ProtegeyClient("key", server.BaseUrl);

        var result = await protegey.Sanctions.ScreenAsync("John Doe", "PERSON");

        Assert.Equal("/partner-api/sanctions/screen", server.LastPathAndQuery);
        Assert.Equal("{\"name\":\"John Doe\",\"type\":\"person\"}", server.LastRequestBody);
        Assert.Equal(0.71, result["score"]);
        Assert.Equal("dfat", ((Dictionary<string, object?>)((List<object?>)result["matches"]!)[0]!)["source"]);
    }

    [Fact]
    public async Task SharedSignal_CheckAsync_ReturnsCategoryOnlyMatches()
    {
        using var server = new FakeServer(201,
            "{\"flagged\":true,\"category\":\"CONFIRMED_FRAUD\",\"reportedDaysAgo\":3,\"flagConfidence\":37,\"matchCount\":2," +
            "\"matches\":[{\"category\":\"CONFIRMED_FRAUD\",\"reportedDaysAgo\":3},{\"category\":\"IDENTITY_THEFT\",\"reportedDaysAgo\":40}]}");
        var protegey = new ProtegeyClient("key", server.BaseUrl);

        var result = await protegey.SharedSignal.CheckAsync(new Dictionary<string, object?>
        {
            ["phoneNumber"] = "+237690000000",
            ["governmentId"] = "ID-1",
        });

        Assert.Equal("/partner-api/shared-signal/check", server.LastPathAndQuery);
        Assert.Equal(2.0, result["matchCount"]);
        Assert.Equal(37.0, result["flagConfidence"]);
        var match = (Dictionary<string, object?>)((List<object?>)result["matches"]!)[1]!;
        Assert.Equal(new[] { "category", "reportedDaysAgo" }, match.Keys.ToArray());
    }
}
