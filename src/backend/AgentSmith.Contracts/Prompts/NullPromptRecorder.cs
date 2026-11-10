namespace AgentSmith.Contracts.Prompts;

/// <summary>2026-11-09-0620a: the recorder of a host without persistence — it keeps nothing.</summary>
public sealed class NullPromptRecorder : IPromptRecorder
{
    public Task<string?> RecordAsync(
        string runId, IReadOnlyList<PromptBlock> blocks, int toolCount, CancellationToken cancellationToken) =>
        Task.FromResult<string?>(null);
}
