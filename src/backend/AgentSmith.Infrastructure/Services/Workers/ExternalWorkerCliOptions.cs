namespace AgentSmith.Infrastructure.Services.Workers;

/// <summary>
/// p0416: how to invoke the external agent CLI for one model call — which binary, which
/// arguments, how long the call may take, and the directory it runs in. The working
/// directory is deliberately NOT a repo: the worker answers a model call, it does not
/// browse the code the run is changing.
/// </summary>
public sealed record ExternalWorkerCliOptions(
    string Binary, IReadOnlyList<string> Arguments, TimeSpan Timeout, string WorkingDirectory);
