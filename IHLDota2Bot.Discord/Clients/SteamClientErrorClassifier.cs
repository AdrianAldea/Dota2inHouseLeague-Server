using Polly.CircuitBreaker;
using Polly.Timeout;

namespace IHLDota2Bot.Discord.Clients;

/// <summary>
/// Classifies failures from a Game Manager HTTP call so they can be logged at an
/// appropriate level instead of every fault being an undifferentiated
/// <c>LogError</c>. The resilience pipeline (timeouts, retries, circuit breaker)
/// surfaces Polly exceptions; transport faults surface as <see cref="HttpRequestException"/>.
/// </summary>
public static class SteamClientErrorClassifier
{
    public enum FailureKind
    {
        /// <summary>The resilience pipeline's per-attempt/total timeout elapsed.</summary>
        Timeout,

        /// <summary>The circuit breaker is open — the backend is being given time to recover.</summary>
        CircuitOpen,

        /// <summary>The caller cancelled (e.g. shutdown); not a backend fault.</summary>
        Canceled,

        /// <summary>A network/transport-level failure reaching the backend.</summary>
        Transport,

        /// <summary>Anything else — genuinely unexpected, worth an error.</summary>
        Unexpected,
    }

    public static FailureKind Classify(Exception ex) =>
        ex switch
        {
            TimeoutRejectedException => FailureKind.Timeout,
            BrokenCircuitException => FailureKind.CircuitOpen,
            OperationCanceledException => FailureKind.Canceled,
            HttpRequestException => FailureKind.Transport,
            _ => FailureKind.Unexpected,
        };

    /// <summary>
    /// Transient/expected conditions (timeout, open circuit, cancellation) log as
    /// warnings; transport and unexpected faults log as errors.
    /// </summary>
    public static LogLevel LevelFor(FailureKind kind) =>
        kind switch
        {
            FailureKind.Timeout => LogLevel.Warning,
            FailureKind.CircuitOpen => LogLevel.Warning,
            FailureKind.Canceled => LogLevel.Warning,
            _ => LogLevel.Error,
        };
}
