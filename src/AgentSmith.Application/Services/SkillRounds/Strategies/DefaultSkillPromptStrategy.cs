using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Services.SkillRounds.Strategies;

/// <summary>
/// p0147d: Default ticket-based domain section used by ticket-driven pipelines
/// (fix-bug, add-feature, MAD) plus the bootstrap path (init-project, where no
/// ticket exists). Renders a synthetic "no ticket" block when ContextKeys.Ticket
/// is absent, mirroring TriageOutputProducer.ResolveTicketOrSyntheticInput.
/// </summary>
public sealed class DefaultSkillPromptStrategy : ISkillPromptStrategy
{
    public string SkillRoundCommandName => "SkillRoundCommand";

    public (string Stable, string PerSkill) BuildDomainSectionParts(PipelineContext pipeline)
    {
        if (!pipeline.TryGet<Ticket>(ContextKeys.Ticket, out var ticket) || ticket is null)
            return ("""
                ## Ticket
                (no ticket — bootstrap or ticketless scan run; rely on the
                ProjectMap and Repository context to ground the response)
                """, string.Empty);
        return ($"""
            ## Ticket
            {ticket.Title}
            {ticket.Description}
            """, string.Empty);
    }
}
