namespace Protegey.Sdk;

/// <summary>
/// Device events — <c>POST /partner-api/device-events</c>. Use this to forward, from your own
/// backend, a device signal collected on the client (e.g. the <c>visitorId</c> produced by a
/// Protegey browser/mobile SDK).
/// </summary>
public sealed class DeviceEventsModule
{
    private readonly Client _http;

    internal DeviceEventsModule(Client http)
    {
        _http = http;
    }

    /// <summary>
    /// Required keys: <c>eventId</c> (idempotency key, globally unique), <c>visitorId</c>.
    /// Optional: <c>externalCustomerId</c>, <c>phoneNumber</c>, <c>deviceAttributes</c> — a
    /// dictionary with any of <c>platform</c>, <c>osVersion</c>, <c>deviceModel</c>,
    /// <c>userAgent</c>, <c>screenWidth</c>, <c>screenHeight</c>, <c>timezone</c>,
    /// <c>language</c>, <c>appVersion</c>, <c>isRooted</c>, <c>isEmulator</c>,
    /// <c>connectionType</c>, <c>devicePixelRatio</c>, <c>manufacturer</c>, <c>cpuCores</c>,
    /// <c>totalMemoryMb</c>, <c>totalStorageMb</c>, <c>freeStorageMb</c>, <c>batteryLevel</c>,
    /// <c>isCharging</c>, <c>isVpnActive</c>.
    ///
    /// <para>Returns <c>{recorded, action, riskScore}</c>; <c>action</c> is <c>ALLOW</c>|
    /// <c>SOFT_CHALLENGE</c>|<c>HARD_CHALLENGE</c>|<c>BLOCK</c>.</para>
    /// </summary>
    public Task<Dictionary<string, object?>> RecordAsync(Dictionary<string, object?> input, CancellationToken cancellationToken = default)
        => _http.PostAsync("/partner-api/device-events", input, cancellationToken);
}
