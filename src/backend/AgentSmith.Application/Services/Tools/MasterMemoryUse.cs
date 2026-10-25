using AgentSmith.Application.Services.Memory;
using AgentSmith.Application.Services.Repositories;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Memory;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-10-25-9324b: one master start's way of naming its memories — entries resolved against the run's stores,
/// and, where the pipeline holds a run id, what it named published for the ledger. Shared by hand_forward's
/// relied_on and dispute_memory, so both read a memory the same way.
/// </summary>
public sealed class MasterMemoryUse(
    PipelineContext pipeline, IRunRepositories repositories, string repoRoot, MemoryCitationResolver resolver,
    MemoryUseRecorder? recorder)
{
    public bool CanRecord => recorder is not null;

    public Task<MemoryCitationRead> ResolveAsync(IReadOnlyList<string> entries, CancellationToken ct) =>
        resolver.ResolveAsync(pipeline, repositories, repoRoot, entries, ct);

    public Task<bool> RecordReliedAsync(IReadOnlyList<MemoryCitation> relied, CancellationToken ct) =>
        recorder?.RecordReliedAsync(relied, ct) ?? Task.FromResult(false);

    public Task<bool> RecordDisputeAsync(MemoryCitation disputed, string reason, CancellationToken ct) =>
        recorder?.RecordDisputeAsync(disputed, reason, ct) ?? Task.FromResult(false);
}
