namespace AgentSmith.Contracts.Services;

/// <summary>
/// Per-run cancellation lookup. The executor calls <see cref="Register"/> at
/// run start to publish a token keyed by runId and <see cref="Unregister"/>
/// in a finally; the cancel endpoint and the watchdog call
/// <see cref="TryCancel"/> by runId. Decouples the cancel surface (HTTP
/// endpoint, watchdog) from the in-flight execution call chain.
/// </summary>
public interface IRunCancellationRegistry
{
    /// <summary>
    /// Registers a fresh CancellationTokenSource linked to <paramref name="parent"/>.
    /// Returns the linked token the caller threads down to the executor.
    /// </summary>
    CancellationToken Register(string runId, CancellationToken parent);

    /// <summary>Returns true if a registration existed and was signalled.</summary>
    bool TryCancel(string runId);

    /// <summary>Removes the entry and disposes its source. Idempotent.</summary>
    void Unregister(string runId);

    /// <summary>
    /// Snapshot of currently-registered runIds plus the timestamp each was
    /// registered at. The watchdog reads this to find overdue runs.
    /// </summary>
    IReadOnlyCollection<RunCancellationEntry> Snapshot();
}

/// <summary>One row in <see cref="IRunCancellationRegistry.Snapshot"/>.</summary>
public sealed record RunCancellationEntry(string RunId, DateTimeOffset RegisteredAt);
