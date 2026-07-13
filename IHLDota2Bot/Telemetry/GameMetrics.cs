using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace IHLDota2Bot.Telemetry;

/// <summary>
/// Domain metrics for the league: lobby/match lifecycle counters, match-duration
/// histogram, and an observable queue-depth gauge. Owns its own <see cref="Meter"/>
/// (name exported so the OpenTelemetry pipeline can subscribe — see DI setup).
/// Registered as a singleton.
/// </summary>
public sealed class GameMetrics : IDisposable
{
    public const string MeterName = "IHLDota2Bot.Game";

    public const string OutcomeCompleted = "completed";
    public const string OutcomeCancelled = "cancelled";

    private readonly Meter meter;
    private readonly Counter<long> lobbiesCreated;
    private readonly Counter<long> matchesCreated;
    private readonly Counter<long> matchesFinished;
    private readonly Histogram<double> matchDuration;
    private volatile Func<int>? queueDepthProvider;

    public GameMetrics()
        : this(MeterName) { }

    // Test-only: a unique meter name isolates measurements when several instances
    // exist concurrently (xUnit runs test classes in parallel).
    internal GameMetrics(string meterName)
    {
        meter = new Meter(meterName);
        lobbiesCreated = meter.CreateCounter<long>(
            "ihl.lobbies.created",
            unit: "{lobby}",
            description: "Lobbies opened in the queue."
        );
        matchesCreated = meter.CreateCounter<long>(
            "ihl.matches.created",
            unit: "{match}",
            description: "Matches persisted when a lobby becomes ready."
        );
        matchesFinished = meter.CreateCounter<long>(
            "ihl.matches.finished",
            unit: "{match}",
            description: "Matches finished, tagged by outcome."
        );
        matchDuration = meter.CreateHistogram<double>(
            "ihl.matches.duration",
            unit: "s",
            description: "Wall-clock time from lobby-ready to finish."
        );
        meter.CreateObservableGauge(
            "ihl.queue.depth",
            ObserveQueueDepth,
            unit: "{player}",
            description: "Players in the current forming lobby."
        );
    }

    public void RecordLobbyCreated() => lobbiesCreated.Add(1);

    public void RecordMatchCreated() => matchesCreated.Add(1);

    public void RecordMatchFinished(string outcome, TimeSpan? duration)
    {
        var tags = new TagList { { "outcome", outcome } };
        matchesFinished.Add(1, tags);
        if (duration is { } d && d >= TimeSpan.Zero)
        {
            matchDuration.Record(d.TotalSeconds, tags);
        }
    }

    /// <summary>
    /// Supplies the live queue depth read by the observable gauge. Set once by the
    /// orchestrator; until then the gauge reports no measurement.
    /// </summary>
    public void SetQueueDepthProvider(Func<int> provider) => queueDepthProvider = provider;

    private IEnumerable<Measurement<int>> ObserveQueueDepth()
    {
        var provider = queueDepthProvider;
        if (provider is null)
            yield break;
        yield return new Measurement<int>(provider());
    }

    public void Dispose() => meter.Dispose();
}
