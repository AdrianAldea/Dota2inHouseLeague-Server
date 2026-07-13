using System.Diagnostics.Metrics;
using IHLDota2Bot.Discord.Telemetry;

namespace IHLDota2Bot.Discord.Tests;

/// <summary>
/// Verifies the command telemetry emits the expected counter/histogram measurements
/// and tags, captured with a <see cref="MeterListener"/> (no external test deps).
/// </summary>
public class DiscordCommandTelemetryTests
{
    private sealed record Captured(
        string Instrument,
        double Value,
        IReadOnlyDictionary<string, object?> Tags
    );

    private static List<Captured> Collect(Action<DiscordCommandTelemetry> act)
    {
        var captured = new List<Captured>();
        using var telemetry = new DiscordCommandTelemetry();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == DiscordCommandTelemetry.MeterName)
                {
                    l.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<long>(
            (inst, val, tags, _) => captured.Add(new(inst.Name, val, ToDict(tags)))
        );
        listener.SetMeasurementEventCallback<double>(
            (inst, val, tags, _) => captured.Add(new(inst.Name, val, ToDict(tags)))
        );
        listener.Start();

        act(telemetry);

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
    public void Handled_RecordsCounterAndDuration()
    {
        var measurements = Collect(t =>
            t.Record("PING", DiscordCommandTelemetry.OutcomeHandled, 12.5)
        );

        var counter = Assert.Single(measurements, m => m.Instrument == "discord.commands.handled");
        Assert.Equal(1, counter.Value);
        Assert.Equal("PING", counter.Tags["command"]);
        Assert.Equal("handled", counter.Tags["outcome"]);

        var duration = Assert.Single(
            measurements,
            m => m.Instrument == "discord.commands.duration"
        );
        Assert.Equal(12.5, duration.Value);
        Assert.Equal("PING", duration.Tags["command"]);
    }

    [Fact]
    public void NonExecutedOutcome_RecordsCounterOnly_NoDuration()
    {
        var measurements = Collect(t =>
            t.Record("STATUS", DiscordCommandTelemetry.OutcomeRateLimited, 0)
        );

        Assert.Single(measurements, m => m.Instrument == "discord.commands.handled");
        Assert.DoesNotContain(measurements, m => m.Instrument == "discord.commands.duration");
    }

    [Fact]
    public void Failed_RecordsDuration()
    {
        var measurements = Collect(t =>
            t.Record("REPORT", DiscordCommandTelemetry.OutcomeFailed, 5.0)
        );

        Assert.Contains(
            measurements,
            m => m.Instrument == "discord.commands.duration" && m.Value == 5.0
        );
        var counter = Assert.Single(measurements, m => m.Instrument == "discord.commands.handled");
        Assert.Equal("failed", counter.Tags["outcome"]);
    }
}
