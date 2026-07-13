using System.Diagnostics.Metrics;
using IHLDota2Bot.Telemetry;

namespace IHLDota2Bot.Tests;

/// <summary>
/// Verifies the domain meter emits the expected counters, the duration histogram,
/// and the observable queue-depth gauge, captured with a BCL <see cref="MeterListener"/>.
/// Each test uses a unique meter name so concurrently-running test classes that also
/// create a <see cref="GameMetrics"/> can't leak measurements into the listener.
/// </summary>
public class GameMetricsTests
{
    private sealed record Captured(
        string Instrument,
        double Value,
        IReadOnlyDictionary<string, object?> Tags
    );

    // Internal ctor (visible via InternalsVisibleTo) lets each test use a unique
    // meter name, isolating it from other test classes that also create GameMetrics.
    private static (GameMetrics Metrics, string MeterName) Create()
    {
        var name = $"IHLDota2Bot.Game.Test.{Guid.NewGuid():N}";
        return (new GameMetrics(name), name);
    }

    private static List<Captured> Collect(string meterName, Action act, bool observe = false)
    {
        var captured = new List<Captured>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == meterName)
                    l.EnableMeasurementEvents(instrument);
            },
        };
        listener.SetMeasurementEventCallback<long>(
            (inst, val, tags, _) => captured.Add(new(inst.Name, val, ToDict(tags)))
        );
        listener.SetMeasurementEventCallback<double>(
            (inst, val, tags, _) => captured.Add(new(inst.Name, val, ToDict(tags)))
        );
        listener.SetMeasurementEventCallback<int>(
            (inst, val, tags, _) => captured.Add(new(inst.Name, val, ToDict(tags)))
        );
        listener.Start();

        act();
        if (observe)
            listener.RecordObservableInstruments();

        listener.Dispose();
        return captured;
    }

    private static Dictionary<string, object?> ToDict(
        ReadOnlySpan<KeyValuePair<string, object?>> tags
    )
    {
        var dict = new Dictionary<string, object?>();
        foreach (var tag in tags)
            dict[tag.Key] = tag.Value;
        return dict;
    }

    [Fact]
    public void RecordLobbyCreated_IncrementsCounter()
    {
        var (metrics, name) = Create();
        using var _ = metrics;

        var measurements = Collect(name, metrics.RecordLobbyCreated);

        var counter = Assert.Single(measurements, m => m.Instrument == "ihl.lobbies.created");
        Assert.Equal(1, counter.Value);
    }

    [Fact]
    public void RecordMatchCreated_IncrementsCounter()
    {
        var (metrics, name) = Create();
        using var _ = metrics;

        var measurements = Collect(name, metrics.RecordMatchCreated);

        Assert.Single(measurements, m => m.Instrument == "ihl.matches.created");
    }

    [Fact]
    public void RecordMatchFinished_RecordsCounterAndDurationWithOutcome()
    {
        var (metrics, name) = Create();
        using var _ = metrics;

        var measurements = Collect(
            name,
            () =>
                metrics.RecordMatchFinished(GameMetrics.OutcomeCompleted, TimeSpan.FromSeconds(42))
        );

        var counter = Assert.Single(measurements, m => m.Instrument == "ihl.matches.finished");
        Assert.Equal("completed", counter.Tags["outcome"]);

        var duration = Assert.Single(measurements, m => m.Instrument == "ihl.matches.duration");
        Assert.Equal(42, duration.Value);
        Assert.Equal("completed", duration.Tags["outcome"]);
    }

    [Fact]
    public void RecordMatchFinished_NegativeDuration_SkipsHistogram()
    {
        var (metrics, name) = Create();
        using var _ = metrics;

        var measurements = Collect(
            name,
            () =>
                metrics.RecordMatchFinished(GameMetrics.OutcomeCancelled, TimeSpan.FromSeconds(-1))
        );

        Assert.Single(measurements, m => m.Instrument == "ihl.matches.finished");
        Assert.DoesNotContain(measurements, m => m.Instrument == "ihl.matches.duration");
    }

    [Fact]
    public void QueueDepthGauge_ReportsProviderValue()
    {
        var (metrics, name) = Create();
        using var _ = metrics;
        metrics.SetQueueDepthProvider(() => 7);

        var measurements = Collect(name, () => { }, observe: true);

        var gauge = Assert.Single(measurements, m => m.Instrument == "ihl.queue.depth");
        Assert.Equal(7, gauge.Value);
    }

    [Fact]
    public void QueueDepthGauge_NoProvider_ReportsNothing()
    {
        var (metrics, name) = Create();
        using var _ = metrics;

        var measurements = Collect(name, () => { }, observe: true);

        Assert.DoesNotContain(measurements, m => m.Instrument == "ihl.queue.depth");
    }
}
