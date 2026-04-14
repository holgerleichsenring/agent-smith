using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

internal static class DeterministicTriageBuilder
{
    public static CommandResult Build(
        PipelineContext pipeline,
        IReadOnlyList<RoleSkillDefinition> roles,
        ISkillGraphBuilder graphBuilder,
        string skillRoundCommandName,
        ILogger logger)
    {
        var graph = graphBuilder.Build(roles);
        pipeline.Set(ContextKeys.SkillGraph, graph);
        pipeline.Set(ContextKeys.ActiveSkill, skillRoundCommandName);

        var commands = new List<PipelineCommand>();
        var stageIndex = 1;

        foreach (var stage in graph.Stages)
        {
            foreach (var skillName in stage.Skills)
            {
                commands.Add(PipelineCommand.SkillRound(skillRoundCommandName, skillName, stageIndex));
            }
            stageIndex++;
        }

        // Debug: log orchestration metadata per skill
        foreach (var role in roles.Where(r => r.Orchestration is not null))
        {
            var o = role.Orchestration!;
            logger.LogDebug(
                "[graph] {Skill}: role={Role}, output={Output}, runs_after=[{After}], runs_before=[{Before}]",
                role.Name, o.Role, o.Output,
                string.Join(", ", o.RunsAfter), string.Join(", ", o.RunsBefore));
        }

        var totalSkills = graph.Stages.Sum(s => s.Skills.Count);
        var stageDescriptions = graph.Stages.Select((s, i) =>
            $"  Stage {i + 1} ({s.RoleLabel}): {string.Join(", ", s.Skills)}");

        logger.LogInformation(
            "Deterministic triage: {StageCount} stages, {SkillCount} skills",
            graph.Stages.Count, totalSkills);
        foreach (var desc in stageDescriptions)
            logger.LogInformation("{StageDescription}", desc);

        var summary = string.Join(" → ",
            graph.Stages.Select(s => $"[{s.RoleLabel}: {string.Join(", ", s.Skills)}]"));

        return CommandResult.OkAndContinueWith(
            $"Deterministic triage: {graph.Stages.Count} stages, {totalSkills} skills — {summary}",
            commands.ToArray());
    }
}
