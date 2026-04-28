using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Shared triage logic: roles check, LLM call, JSON parsing, SkillRound insertion.
/// </summary>
public abstract class TriageHandlerBase
{
    protected abstract ILogger Logger { get; }
    protected abstract IPromptCatalog Prompts { get; }
    protected abstract string BuildUserPrompt(PipelineContext pipeline);
    protected virtual string SkillRoundCommandName => "SkillRoundCommand";
    protected virtual ISkillGraphBuilder? GraphBuilder => null;

    protected async Task<CommandResult> TriageAsync(
        PipelineContext pipeline, ILlmClient llmClient, CancellationToken cancellationToken)
    {
        if (!pipeline.TryGet<IReadOnlyList<RoleSkillDefinition>>(
                ContextKeys.AvailableRoles, out var roles) || roles is null || roles.Count == 0)
        {
            Logger.LogInformation("No available roles for triage, skipping discussion");
            return CommandResult.Ok("No roles available, skipping triage");
        }

        if (pipeline.TryGet<PipelineType>(ContextKeys.PipelineTypeName, out var pipelineType)
            && pipelineType is PipelineType.Structured or PipelineType.Hierarchical
            && GraphBuilder is not null)
        {
            return DeterministicTriageBuilder.Build(
                pipeline, roles, GraphBuilder, SkillRoundCommandName, Logger);
        }

        var triageResult = await CallLlmTriageAsync(pipeline, roles, llmClient, cancellationToken);
        if (triageResult is null || triageResult.Participants.Count == 0)
        {
            Logger.LogInformation("Triage returned no participants, skipping discussion");
            return CommandResult.Ok("Triage: no roles needed, skipping discussion");
        }

        EnsureMandatoryRoles(roles, triageResult);

        if (triageResult.Participants.Count == 1)
        {
            Logger.LogInformation("Single role needed: {Role}", triageResult.Participants[0]);
            return CommandResult.Ok(
                $"Triage: single role ({triageResult.Participants[0]}), no discussion needed");
        }

        return BuildSkillRoundCommands(pipeline, triageResult);
    }

    private async Task<TriageResult?> CallLlmTriageAsync(
        PipelineContext pipeline, IReadOnlyList<RoleSkillDefinition> roles,
        ILlmClient llmClient, CancellationToken cancellationToken)
    {
        var rolesDescription = string.Join("\n", roles.Select(r =>
            $"- {r.Name}: {r.Description} (triggers: {string.Join(", ", r.Triggers)})"));

        var parser = new TriageResponseParser(Prompts, Logger);
        var fullPrompt = parser.BuildPrompt(BuildUserPrompt(pipeline), rolesDescription);

        try
        {
            var llmResponse = await llmClient.CompleteAsync(
                parser.SystemPrompt, fullPrompt, TaskType.Planning, cancellationToken);
            PipelineCostTracker.GetOrCreate(pipeline).Track(llmResponse);
            return parser.Parse(llmResponse.Text, roles);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Triage LLM call failed, skipping discussion");
            return null;
        }
    }

    private void EnsureMandatoryRoles(
        IReadOnlyList<RoleSkillDefinition> roles, TriageResult triageResult)
    {
        foreach (var role in roles.Where(r => r.Triggers.Contains("always_include")))
        {
            if (triageResult.Participants.Contains(role.Name)) continue;
            triageResult.Participants.Add(role.Name);
            Logger.LogInformation("Added mandatory role {Role} (always_include trigger)", role.Name);
        }
    }

    private CommandResult BuildSkillRoundCommands(PipelineContext pipeline, TriageResult triage)
    {
        pipeline.Set(ContextKeys.ActiveSkill, SkillRoundCommandName);

        var commands = new List<PipelineCommand>
        {
            PipelineCommand.SkillRound(SkillRoundCommandName, triage.Lead, 1)
        };
        commands.AddRange(triage.Participants
            .Where(p => p != triage.Lead)
            .Select(p => PipelineCommand.SkillRound(SkillRoundCommandName, p, 1)));
        commands.Add(PipelineCommand.Simple(CommandNames.ConvergenceCheck));

        Logger.LogInformation("Triage complete. Lead: {Lead}, Participants: {Participants}",
            triage.Lead, string.Join(", ", triage.Participants));
        foreach (var (role, reason) in triage.Reasoning)
        {
            var status = triage.Participants.Contains(role) ? "INCLUDED" : "EXCLUDED";
            Logger.LogInformation("  [{Status}] {Role}: {Reason}", status, role, reason);
        }

        return CommandResult.OkAndContinueWith(
            $"Triage complete. Lead: {triage.Lead}. " +
            $"Participants: {string.Join(", ", triage.Participants)}",
            commands.ToArray());
    }
}
