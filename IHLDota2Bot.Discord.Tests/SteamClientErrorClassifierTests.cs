using IHLDota2Bot.Discord.Clients;
using Microsoft.Extensions.Logging;
using Polly.CircuitBreaker;
using Polly.Timeout;
using static IHLDota2Bot.Discord.Clients.SteamClientErrorClassifier;

namespace IHLDota2Bot.Discord.Tests;

/// <summary>
/// Maps the exceptions a resilient HTTP call can throw to a failure kind + log level,
/// so transient timeouts / open circuits aren't logged as hard errors.
/// </summary>
public class SteamClientErrorClassifierTests
{
    [Fact]
    public void TimeoutRejected_IsTimeout() =>
        Assert.Equal(FailureKind.Timeout, Classify(new TimeoutRejectedException()));

    [Fact]
    public void BrokenCircuit_IsCircuitOpen() =>
        Assert.Equal(FailureKind.CircuitOpen, Classify(new BrokenCircuitException()));

    [Fact]
    public void TaskCanceled_IsCanceled() =>
        Assert.Equal(FailureKind.Canceled, Classify(new TaskCanceledException()));

    [Fact]
    public void OperationCanceled_IsCanceled() =>
        Assert.Equal(FailureKind.Canceled, Classify(new OperationCanceledException()));

    [Fact]
    public void HttpRequest_IsTransport() =>
        Assert.Equal(FailureKind.Transport, Classify(new HttpRequestException("boom")));

    [Fact]
    public void Other_IsUnexpected() =>
        Assert.Equal(FailureKind.Unexpected, Classify(new InvalidOperationException()));

    [Theory]
    [InlineData(FailureKind.Timeout, LogLevel.Warning)]
    [InlineData(FailureKind.CircuitOpen, LogLevel.Warning)]
    [InlineData(FailureKind.Canceled, LogLevel.Warning)]
    [InlineData(FailureKind.Transport, LogLevel.Error)]
    [InlineData(FailureKind.Unexpected, LogLevel.Error)]
    public void LevelFor_MapsTransientToWarning_AndFaultsToError(FailureKind kind, LogLevel expected) =>
        Assert.Equal(expected, LevelFor(kind));
}
