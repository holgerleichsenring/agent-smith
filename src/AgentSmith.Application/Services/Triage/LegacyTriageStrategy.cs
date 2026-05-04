using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Triage;

/// <summary>
/// LLM-based open-discussion triage for Discussion-type pipelines (legal-analysis,
/// mad-discussion, init-project, skill-manager, autonomous). Picks Lead + Participants
/// from available roles and emits a flat SkillRound + ConvergenceCheck sequence.
/// Phase model not applied — discussion runs as one open round.
/// </summary>
public sealed class LegacyTriageStrategy(
    IPromptCatalog prompts,
    ILogger<LegacyTriageStrategy> logger) : ITriageStrategy
{
    public async Task<CommandResult> ExecuteAsync(
        PipelineContext pipeline, ILlmClient llmClient, CancellationToken cancellationToken)
    {
        if (!TryLoadRoles(pipeline, out var roles))
            return CommandResult.Ok("No roles available, skipping triage");

        var triage = await CallLlmAsync(pipeline, roles, llmClient, cancellationToken);
        if (triage is null || triage.Participants.Count == 0)
            return CommandResult.Ok("Triage: no roles needed, skipping discussion");

        EnsureMandatoryRoles(roles, triage);

        if (triage.Participants.Count == 1)
            return CommandResult.Ok(
                $"Triage: single role ({triage.Participants[0]}), no discussion needed");

        return BuildCommands(pipeline, triage);
    }

    private bool TryLoadRoles(
        PipelineContext pipeline, out IReadOnlyList<RoleSkillDefinition> roles)
    {
        if (!pipeline.TryGet<IReadOnlyList<RoleSkillDefinition>>(
                ContextKeys.AvailableRoles, out var loaded) || loaded is null || loaded.Count == 0)
        {
            logger.LogInformation("No available roles for triage, skipping discussion");
            roles = Array.Empty<RoleSkillDefinition>();
            return false;
        }
        roles = loaded;
        return true;
    }

    private async Task<TriageResult?> CallLlmAsync(
        PipelineContext pipeline, IReadOnlyList<RoleSkillDefinition> roles,
        ILlmClient llmClient, CancellationToken cancellationToken)
    {
        var rolesDescription = string.Join("\n", roles.Select(r =>
            $"- {r.Name}: {r.Description} (triggers: {string.Join(", ", r.Triggers)})"));

        var parser = new TriageResponseParser(prompts, logger);
        var fullPrompt = parser.BuildPrompt(BuildUserPrompt(pipeline), rolesDescription);

        try
        {
            var response = await llmClient.CompleteAsync(
                parser.SystemPrompt, fullPrompt, TaskType.Planning, cancellationToken);
            PipelineCostTracker.GetOrCreate(pipeline).Track(response);
            return parser.Parse(response.Text, roles);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Triage LLM call failed, skipping discussion");
            return null;
        }
    }

    private static string BuildUserPrompt(PipelineContext pipeline)
    {
        var ticket = pipeline.Get<Ticket>(ContextKeys.Ticket);
        pipeline.TryGet<string>(ContextKeys.ProjectContext, out var projectContext);
        return $"""
            ## Ticket
            {ticket.Title}
            {ticket.Description}

            ## Project Context
            {projectContext ?? "Not available"}
            """;
    }

    private void EnsureMandatoryRoles(
        IReadOnlyList<RoleSkillDefinition> roles, TriageResult triage)
    {
        foreach (var role in roles.Where(r => r.Triggers.Contains("always_include")))
        {
            if (triage.Participants.Contains(role.Name)) continue;
            triage.Participants.Add(role.Name);
            logger.LogInformation("Added mandatory role {Role} (always_include trigger)", role.Name);
        }
    }

    private CommandResult BuildCommands(PipelineContext pipeline, TriageResult triage)
    {
        pipeline.Set(ContextKeys.ActiveSkill, CommandNames.SkillRound);

        var commands = new List<PipelineCommand>
        {
            PipelineCommand.SkillRound(CommandNames.SkillRound, triage.Lead, 1)
        };
        commands.AddRange(triage.Participants
            .Where(p => p != triage.Lead)
            .Select(p => PipelineCommand.SkillRound(CommandNames.SkillRound, p, 1)));
        commands.Add(PipelineCommand.Simple(CommandNames.ConvergenceCheck));

        logger.LogInformation("Triage complete. Lead: {Lead}, Participants: {Participants}",
            triage.Lead, string.Join(", ", triage.Participants));

        return CommandResult.OkAndContinueWith(
            $"Triage complete. Lead: {triage.Lead}. " +
            $"Participants: {string.Join(", ", triage.Participants)}",
            commands.ToArray());
    }
}
