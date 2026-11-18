namespace Protegey.Sdk;

/// <summary>
/// Behavioral biometrics — <c>POST /partner-api/behavioral-events</c>. Forward a session's
/// aggregated behavior (collected on the client) from your backend.
/// </summary>
public sealed class BehavioralEventsModule
{
    private readonly Client _http;

    internal BehavioralEventsModule(Client http)
    {
        _http = http;
    }

    /// <summary>
    /// Required keys: <c>externalCustomerId</c>, <c>sessionId</c>. Optional (each a dictionary):
    /// <c>keystroke</c> {<c>avgHoldTimeMs</c>, <c>avgInterKeyLatencyMs</c>,
    /// <c>typingSpeedCharsPerSec</c>, <c>errorRate</c>}, <c>touch</c> {<c>avgTapPressure</c>,
    /// <c>avgSwipeVelocity</c>, <c>scrollBehaviorScore</c>, <c>mouseAcceleration</c>},
    /// <c>navigation</c> {<c>screenSequence</c>: string list}, <c>session</c>
    /// {<c>loginHourBucket</c> 0–23, <c>loginDayOfWeek</c> 0–6, <c>sessionDurationMs</c>}.
    ///
    /// <para>Returns <c>{status, deviationScore, confidenceTier, stepUpRecommended,
    /// escalatedAlertId}</c>; <c>status</c> is <c>LEARNING</c> (baseline still being built) or
    /// <c>SCORED</c>, <c>confidenceTier</c> is <c>LOW</c>|<c>MEDIUM</c>|<c>HIGH</c> or null.</para>
    /// </summary>
    public Task<Dictionary<string, object?>> RecordAsync(Dictionary<string, object?> input, CancellationToken cancellationToken = default)
        => _http.PostAsync("/partner-api/behavioral-events", input, cancellationToken);
}
