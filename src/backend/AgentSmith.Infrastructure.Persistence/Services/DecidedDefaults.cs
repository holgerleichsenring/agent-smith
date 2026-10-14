using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// 2026-10-13-8762p: the decided default rows of a ticket in one query — those of its code runs, and those of
/// the bound design conversation when one is named. Ordered by the decision instant in memory: SQLite cannot
/// ORDER BY a DateTimeOffset.
/// </summary>
public sealed class DecidedDefaults(IServiceScopeFactory scopeFactory) : IDecidedDefaults
{
    public async Task<IReadOnlyList<DecidedDefault>> ReadAsync(
        string project, string ticketId, string? conversation, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(project) || string.IsNullOrWhiteSpace(ticketId)) return [];
        using var scope = scopeFactory.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var codeRuns = uow.Set<Run>().Where(r => r.Project == project && r.TicketId == ticketId
            && r.Pipeline == PipelinePresets.CodeName).Select(r => r.Id);
        var rows = await uow.Set<RunDefaultDecision>().AsNoTracking()
            .Where(d => d.Verdict != DefaultDecisionVerdicts.Pending
                && (codeRuns.Contains(d.RunId) || (conversation != null && d.Conversation == conversation)))
            .ToListAsync(cancellationToken);
        return [.. rows.Where(d => d.VerdictAt is not null).OrderBy(d => d.VerdictAt).ThenBy(d => d.Id)
            .Select(d => new DecidedDefault(d.Id, d.RunId, d.SpecId, d.Question, d.Chosen,
                d.Verdict, d.VerdictNote, d.VerdictBy ?? string.Empty, d.VerdictAt!.Value))];
    }
}
