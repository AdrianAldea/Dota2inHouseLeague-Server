using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace IHLDota2Bot.Discord.Telemetry;

/// <summary>
/// Owns the command-pipeline <see cref="Meter"/> and <see cref="ActivitySource"/>
/// and the instruments the router records to. Registered as a singleton; the meter
/// and source names are exported so the OpenTelemetry pipeline can subscribe to them
/// (see <c>Program.cs</c>).
/// </summary>
public sealed class DiscordCommandTelemetry : IDisposable
{
    public const string MeterName = "IHLDota2Bot.Discord.Commands";
    public const string ActivitySourceName = "IHLDota2Bot.Discord.Commands";

    // Outcome tag values recorded on the counter.
    public const string OutcomeHandled = "handled";
    public const string OutcomeFailed = "failed";
    public const string OutcomeDenied = "denied";
    public const string OutcomeRateLimited = "rate_limited";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    private readonly Meter meter;
    private readonly Counter<long> handled;
    private readonly Histogram<double> duration;

    public DiscordCommandTelemetry()
    {
        meter = new Meter(MeterName);
        handled = meter.CreateCounter<long>(
            "discord.commands.handled",
            unit: "{command}",
            description: "Number of Discord commands processed, tagged by command and outcome.");
        duration = meter.CreateHistogram<double>(
            "discord.commands.duration",
            unit: "ms",
            description: "Handler execution time per Discord command.");
    }

    /// <summary>Records the throughput counter and, for executed commands, the duration histogram.</summary>
    public void Record(string command, string outcome, double elapsedMs)
    {
        handled.Add(1, new TagList { { "command", command }, { "outcome", outcome } });

        // Duration only makes sense for commands that actually ran a handler.
        if (outcome is OutcomeHandled or OutcomeFailed)
        {
            duration.Record(elapsedMs, new TagList { { "command", command } });
        }
    }

    public void Dispose() => meter.Dispose();
}
