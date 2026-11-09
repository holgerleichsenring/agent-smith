namespace AgentSmith.Contracts.Prompts;

/// <summary>
/// 2026-11-09-0620a: keeps what one model call was sent, so anyone can read the prompt a run's model actually
/// saw. Called before every provider call; a failure to record never fails the call.
/// </summary>
public interface IPromptRecorder
{
    /// <summary>Stores the call's blocks and returns the id the call's finished event carries, or null when
    /// nothing was stored.</summary>
    Task<string?> RecordAsync(
        string runId, IReadOnlyList<PromptBlock> blocks, int toolCount, CancellationToken cancellationToken);
}
