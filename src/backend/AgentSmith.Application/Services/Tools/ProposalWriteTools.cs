using AgentSmith.Application.Models;
using AgentSmith.Application.Services.SpecDialog.Writes;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-11-12-cd4ca: the design master's write tools over its turn's proposal store. 2026-11-15-06fc: the draft is
/// files — write_file, edit_file and delete_file — checked against the turn's own repositories, and a comment is a
/// file of the draft only on a conversation bound to a ticket.
/// </summary>
public sealed class ProposalWriteTools(DraftFileWrite files)
{
    /// <summary>Every tool name, so a surface that never proposes can leave them out.</summary>
    public static readonly IReadOnlySet<string> Names = new HashSet<string>(StringComparer.Ordinal)
    {
        DraftFileToolHost.WriteName, DraftFileToolHost.EditName, DraftFileToolHost.DeleteName,
    };

    public IEnumerable<AITool> For(PipelineContext pipeline, ProposalStore store)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(store);
        IReadOnlyList<string> repos = [.. DerivationLookFactory.ProposalRepositories(pipeline)?.Keys ?? []];
        var bound = pipeline.TryGet<SeededTicket>(ContextKeys.SpecDialogTicket, out var ticket) && ticket is not null;
        return new DraftFileToolHost(store, files, new DraftScope(repos, bound)).GetTools(null, null);
    }
}
