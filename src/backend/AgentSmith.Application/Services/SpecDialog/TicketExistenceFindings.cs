using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// 2026-10-14-dbcek: every ticket:&lt;id&gt; a proposed list requires, read for existence on the project's tracker —
/// one finding per missing or unreadable id, under each ticket that requires it. 2026-11-12-cd4ci: a mechanical fact,
/// so it runs whether or not the plan is reviewed. A host that registers no existence read checks nothing.
/// </summary>
public sealed class TicketExistenceFindings(ILogger<TicketExistenceFindings> logger, ITicketExistence? existence = null)
{
    public async Task<IReadOnlyList<ProposalFinding>> FindingsAsync(PipelineContext pipeline, TicketGraphOutcome list, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(list);
        var required = list.Tickets.SelectMany(t => t.Requires.Select(r => (t.Key, Id: GraphTicketRequire.ExistingId(r))))
            .Where(r => !string.IsNullOrEmpty(r.Id)).ToList();
        if (existence is null || required.Count == 0
            || !pipeline.TryGet<ResolvedProject>(ContextKeys.SpecDialogProject, out var project) || project is null) return [];
        TicketExistenceResult result;
        try
        {
            result = await existence.CheckAsync(project, [.. required.Select(r => r.Id!).Distinct(StringComparer.Ordinal)], ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "The tickets the proposed list requires could not be checked");
            return [];
        }
        var unreadable = result.Unreadable.ToDictionary(u => u.Id, u => u.Reason, StringComparer.Ordinal);
        return [.. required.Select(r => result.Missing.Contains(r.Id!)
                ? new ProposalFinding(r.Key, "required ticket not found", $"ticket {r.Id} does not exist on {project.Tracker.Name}")
                : unreadable.TryGetValue(r.Id!, out var reason)
                    ? new ProposalFinding(r.Key, "required ticket not read", $"ticket {r.Id} could not be read ({reason})")
                    : null).OfType<ProposalFinding>()];
    }
}
