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

    /// <summary>2026-11-17-166ae: the binary is Claude Code's CLI, so the prompt's system part goes in through
    /// <c>--system-prompt-file</c> — cached across calls — and only the input part on stdin.</summary>
    public bool SystemPromptAsFile { get; init; }

    /// <summary>2026-11-17-d6f5: with the system file, the input goes on stdin as one stream-json user message holding
    /// the prompt's input blocks, under <c>--session-id</c> of the conversation — the CLI then reads from cache what
    /// the conversation's previous call wrote.</summary>
    public bool InputAsBlocks { get; init; }

    /// <summary>2026-11-17-cb32b: variables set on the worker process, over what it inherits from the server.</summary>
    public IReadOnlyDictionary<string, string> Environment { get; init; } = new Dictionary<string, string>();
}
