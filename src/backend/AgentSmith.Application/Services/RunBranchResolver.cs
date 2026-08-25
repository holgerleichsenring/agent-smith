using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services;

/// <summary>
/// p0496: one answer to "which branch does this run check out, and did the run compose it
/// itself?". The initial CheckoutSource step and the mid-run ensure_repo_sandbox
/// escalation both ask here, so they cannot answer it differently — and the answer to the
/// second half is what licenses writing to the branch at all.
/// </summary>
public static class RunBranchResolver
{
    public static RunBranch? Resolve(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (pipeline.TryGet<string>(ContextKeys.CheckoutBranch, out var handed)
            && !string.IsNullOrWhiteSpace(handed))
            return new RunBranch(new BranchName(handed), ComposedFromTicket: false);

        return pipeline.TryGet<TicketId>(ContextKeys.TicketId, out var ticketId) && ticketId is not null
            ? new RunBranch(TicketBranchNamer.Compose(ticketId), ComposedFromTicket: true)
            : null;
    }
}
