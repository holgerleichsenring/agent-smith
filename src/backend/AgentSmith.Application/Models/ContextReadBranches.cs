using AgentSmith.Application.Services;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Models;

/// <summary>
/// 2026-10-12-6bfaa: the branches a pre-sandbox context read tries BEFORE the default branch, in
/// order — the run's own branch, then the parent's rung when the run is a slice. The default
/// branch is always the last resort and is never listed.
/// <para>
/// A run's toolchain image comes from the context.yaml it reads here. Read on the default branch
/// alone, a context that exists only on the ticket branch — an init that is not merged yet — is
/// invisible, and the sandbox lands on the generic image while the checkout holds the context.
/// </para>
/// </summary>
public sealed record ContextReadBranches(IReadOnlyList<BranchName> Candidates)
{
    /// <summary>No run in hand: the default branch alone, as every read was before.</summary>
    public static ContextReadBranches None { get; } = new([]);

    public static ContextReadBranches ForBranch(BranchName branch) => new([branch]);

    /// <summary>The branch the run checks out, then the rung it would be cut from.</summary>
    public static ContextReadBranches For(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (RunBranchResolver.Resolve(pipeline) is not { } run) return None;
        var candidates = new List<BranchName> { run.Name };
        if (!string.IsNullOrWhiteSpace(run.ParentTicketId))
            candidates.Add(TicketBranchNamer.Compose(new TicketId(run.ParentTicketId.Trim())));
        return new ContextReadBranches(candidates);
    }
}
