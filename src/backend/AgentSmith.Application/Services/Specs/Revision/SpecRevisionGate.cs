using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs.Revision;

/// <summary>
/// 2026-11-17-ec48a: when the coding master may revise its current spec. Not inside the one repair pass or the
/// review's one fix pass — their bound and their revert are measured against the spec they started on — and not while
/// a checkout's index holds staged work, which the spec commit (it commits the whole index) would carry onto the branch.
/// The refusal names what to do instead; null admits the revision.
/// </summary>
public sealed class SpecRevisionGate(SandboxGitOperations git)
{
    public async Task<string?> RefusalAsync(PipelineContext pipeline, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (!pipeline.TryGet<SpecDraft>(ContextKeys.CurrentSpec, out var spec) || spec is null
            || !pipeline.TryGet<SpecSet>(ContextKeys.SpecSet, out var set) || set is null)
            return "this run is not building a spec";
        if (pipeline.Has(ContextKeys.OutstandingCriteria))
            return $"this pass closes findings measured against spec {spec.SpecId} as it stands; finish it, or ask with "
                + "ask_human when the operator's message changes what the spec must make true";
        return await StagedAsync(pipeline, ct);
    }

    private async Task<string?> StagedAsync(PipelineContext pipeline, CancellationToken ct)
    {
        if (!pipeline.TryGet<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes, out var sandboxes) || sandboxes is null)
            return null;
        foreach (var (key, sandbox) in sandboxes)
            if (await git.GetStagedFileNamesAsync(sandbox, ct) is { Count: > 0 } staged)
                return $"{key} has staged changes ({string.Join(", ", staged)}) that the spec commit would carry; "
                    + "unstage them (git reset) or commit them, then revise again";
        return null;
    }
}
