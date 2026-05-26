using AgentSmith.Application.Services.Activation;
using AgentSmith.Contracts.Activation;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Triage;

/// <summary>
/// Post-p0143 thin wrapper around <see cref="DeterministicTriageSelector"/>.
/// Loads available skills, narrows via <see cref="ActivationSkillFilter"/>,
/// runs deterministic selection, trims per-phase via the specificity
/// trimmer, and applies ticket-label overrides. No LLM call in this path —
/// vocabulary + role-slot validity are guaranteed by construction
/// (the selector only assigns skills from the candidate pool with matching
/// roles).
/// </summary>
public sealed class TriageOutputProducer(
    DeterministicTriageSelector selector,
    TriageLabelOverrideApplier labelOverrider,
    ActivationSkillFilter activationFilter,
    PhaseSpecificityTrimmer phaseTrimmer,
    Func<PipelineContext, IRunStateConcepts> conceptsFactory,
    LoopLimitsConfig limits,
    ILogger<TriageOutputProducer> logger) : ITriageOutputProducer
{
    public Task<TriageOutput> ProduceAsync(
        PipelineContext pipeline, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var skills = LoadAvailableSkills(pipeline);
        var filtered = activationFilter.Filter(skills, conceptsFactory(pipeline));
        logger.LogInformation(
            "Triage filtered {Before}->{After} skills via activates_when", skills.Count, filtered.Count);

        var selected = selector.Select(filtered);
        var trimmed = phaseTrimmer.Trim(selected, filtered, limits.MaxSkillsPerPhase);
        var ticketLabels = ResolveTicketLabels(pipeline);
        var withOverrides = labelOverrider.Apply(trimmed, ticketLabels);
        LogUnderCount(withOverrides);
        return Task.FromResult(withOverrides);
    }

    private static IReadOnlyList<RoleSkillDefinition> LoadAvailableSkills(PipelineContext pipeline)
        => pipeline.TryGet<IReadOnlyList<RoleSkillDefinition>>(
            ContextKeys.AvailableRoles, out var roles) && roles is not null
            ? roles
            : Array.Empty<RoleSkillDefinition>();

    private static IReadOnlyList<string> ResolveTicketLabels(PipelineContext pipeline)
        => pipeline.TryGet<AgentSmith.Domain.Entities.Ticket>(ContextKeys.Ticket, out var ticket)
            && ticket is not null
            ? ticket.Labels
            : Array.Empty<string>();

    private void LogUnderCount(TriageOutput output)
    {
        foreach (var (phase, assignment) in output.Phases)
        {
            if (IsRequiredSlotMissing(phase, assignment))
                logger.LogWarning(
                    "Triage phase {Phase}: required slot under-filled — Lead={Lead}, Filter={Filter}",
                    phase, assignment.Lead ?? "(empty)", assignment.Filter ?? "(empty)");
        }
    }

    private static bool IsRequiredSlotMissing(PipelinePhase phase, PhaseAssignment a)
        => phase switch
        {
            PipelinePhase.Plan => a.Lead is null,
            PipelinePhase.Review => a.Reviewers.Count == 0,
            PipelinePhase.Final => a.Filter is null,
            _ => false
        };
}
