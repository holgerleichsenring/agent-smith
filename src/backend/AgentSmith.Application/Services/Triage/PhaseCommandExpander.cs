using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Triage;

/// <summary>
/// Translates a TriageOutput phase entry into the flat sequence of pipeline commands
/// the executor consumes. Lead/Analyst/Reviewer assignments emit SkillRoundCommand;
/// Filter assignment emits FilterRoundCommand. Empty phase yields no commands.
/// </summary>
public sealed class PhaseCommandExpander
{
    public IReadOnlyList<PipelineCommand> ExpandPhase(
        TriageOutput triage, PipelinePhase phase, int round, string skillRoundCommandName)
    {
        if (!triage.Phases.TryGetValue(phase, out var assignment))
            return Array.Empty<PipelineCommand>();

        var commands = new List<PipelineCommand>();
        if (assignment.Lead is not null)
            commands.Add(PipelineCommand.SkillRound(skillRoundCommandName, assignment.Lead, round));
        commands.AddRange(assignment.Analysts.Select(name =>
            PipelineCommand.SkillRound(skillRoundCommandName, name, round)));
        commands.AddRange(assignment.Reviewers.Select(name =>
            PipelineCommand.SkillRound(skillRoundCommandName, name, round)));
        if (assignment.Filter is not null)
            commands.Add(PipelineCommand.SkillRound(CommandNames.FilterRound, assignment.Filter, round));
        return commands;
    }
}
