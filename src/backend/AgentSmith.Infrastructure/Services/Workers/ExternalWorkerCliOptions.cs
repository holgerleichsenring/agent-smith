namespace AgentSmith.Infrastructure.Services.Workers;

/// <summary>
/// p0416: how to invoke the external agent CLI for one model call — which binary, which
/// arguments, how long the call may take, and the directory it runs in. The working
/// directory is deliberately NOT a repo: the worker answers a model call, it does not
/// browse the code the run is changing.
/// </summary>
public sealed record ExternalWorkerCliOptions(
    string Binary, IReadOnlyList<string> Arguments, TimeSpan Timeout, string WorkingDirectory)
{
    /// <summary>
    /// p0419: base pause before re-asking a worker whose PROCESS failed; grows with the
    /// attempt. Configurable so a test can prove the retry without waiting out a
    /// production backoff.
    /// </summary>
    public TimeSpan RetryPause { get; init; } = TimeSpan.FromSeconds(2);
}
