using AgentSmith.Application.Models;
using AgentSmith.Application.Services.SpecDialog.Writes;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-11-12-cd4ca: the design master's write tools over its turn's proposal store — write_spec, write_contract,
/// write_ticket, write_bug, remove_item, and write_comment on a conversation bound to a ticket. The repositories each
/// write is checked against are the turn's own.
/// </summary>
public sealed class ProposalWriteTools(TicketDraftWrite tickets, SpecDraftWrite specs, ContractDraftWrite contracts)
{
    /// <summary>Every tool name, so a surface that never proposes can leave them out.</summary>
    public static readonly IReadOnlySet<string> Names = new HashSet<string>(StringComparer.Ordinal)
    {
        SpecWriteToolHost.Name, SpecWriteToolHost.ContractName, TicketWriteToolHost.Name, BugWriteToolHost.Name,
        CommentWriteToolHost.Name, ProposalItemToolHost.Name,
    };

    public IEnumerable<AITool> For(PipelineContext pipeline, ProposalStore store)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(store);
        IReadOnlyList<string> scope = [.. DerivationLookFactory.ProposalRepositories(pipeline)?.Keys ?? []];
        var tools = new List<AITool>();
        tools.AddRange(new SpecWriteToolHost(store, specs, contracts, scope).GetTools(null, null));
        tools.AddRange(new TicketWriteToolHost(store, tickets, scope).GetTools(null, null));
        tools.AddRange(new BugWriteToolHost(store).GetTools(null, null));
        if (pipeline.TryGet<SeededTicket>(ContextKeys.SpecDialogTicket, out var bound) && bound is not null)
            tools.AddRange(new CommentWriteToolHost(store).GetTools(null, null));
        tools.AddRange(new ProposalItemToolHost(store).GetTools(null, null));
        return tools;
    }
}
