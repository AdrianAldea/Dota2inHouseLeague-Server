namespace IHLDota2Bot.Discord.Tests.TestSupport;

/// <summary>
/// A <see cref="TimeProvider"/> whose clock only moves when the test advances it,
/// so time-based logic (rate-limit windows) is deterministic.
/// </summary>
internal sealed class TestTimeProvider(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset now = start;

    public TestTimeProvider() : this(DateTimeOffset.UnixEpoch) { }

    public override DateTimeOffset GetUtcNow() => now;

    public void Advance(TimeSpan by) => now += by;
}
