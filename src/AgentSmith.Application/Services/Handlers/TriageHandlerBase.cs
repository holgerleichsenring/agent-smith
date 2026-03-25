using System.Text.Json;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Shared triage logic: roles check, LLM call, JSON parsing, SkillRound insertion.
/// Subclasses provide the domain-specific prompt (ticket vs code analysis).
/// </summary>
public abstract class TriageHandlerBase
{
    protected abstract ILogger Logger { get; }
    protected abstract string BuildUserPrompt(PipelineContext pipeline);
    protected virtual string SkillRoundCommandName => "SkillRoundCommand";

    protected async Task<CommandResult> TriageAsync(
        PipelineContext pipeline,
        ILlmClient llmClient,
        CancellationToken cancellationToken)
    {
        if (!pipeline.TryGet<IReadOnlyList<RoleSkillDefinition>>(
                ContextKeys.AvailableRoles, out var roles) || roles is null || roles.Count == 0)
        {
            Logger.LogInformation("No available roles for triage, skipping discussion");
            return CommandResult.Ok("No roles available, skipping triage");
        }

        var userPrompt = BuildUserPrompt(pipeline);

        var rolesDescription = string.Join("\n", roles.Select(r =>
            $"- {r.Name}: {r.Description} (triggers: {string.Join(", ", r.Triggers)})"));

        var fullPrompt = $$"""
            {{userPrompt}}

            ## Available Roles
            {{rolesDescription}}

            ## Instructions
            Analyze the input and determine:
            1. Which roles are needed (select from available roles only)
            2. Who should lead the discussion (creates the initial analysis)
            3. For EVERY available role, explain why it is included or excluded

            Respond in JSON:
            {
              "lead": "role-name",
              "participants": ["role-name-1", "role-name-2"],
              "reasoning": {
                "role-name-1": "why this role is needed",
                "role-name-2": "why this role is needed",
                "excluded-role": "why this role is not needed"
              }
            }
            """;

        var systemPrompt = "You are triaging work to determine which specialist roles " +
                           "should participate. Respond with valid JSON only, no markdown.";

        TriageResult? triageResult;
        try
        {
            var llmResponse = await llmClient.CompleteAsync(
                systemPrompt, fullPrompt, TaskType.Planning, cancellationToken);
            PipelineCostTracker.GetOrCreate(pipeline).Track(llmResponse);
            triageResult = ParseTriageResponse(llmResponse.Text, roles);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Triage LLM call failed, skipping discussion");
            return CommandResult.Ok("Triage: no roles needed, skipping discussion");
        }

        if (triageResult is null || triageResult.Participants.Count == 0)
        {
            Logger.LogInformation("Triage returned no participants, skipping discussion");
            return CommandResult.Ok("Triage: no roles needed, skipping discussion");
        }

        if (triageResult.Participants.Count == 1)
        {
            Logger.LogInformation("Single role needed: {Role}, skipping discussion",
                triageResult.Participants[0]);
            return CommandResult.Ok(
                $"Triage: single role ({triageResult.Participants[0]}), no discussion needed");
        }

        // Store the skill round command name so ConvergenceCheck can use it for re-insertion
        pipeline.Set(ContextKeys.ActiveSkill, SkillRoundCommandName);

        var commandsToInsert = new List<PipelineCommand>();
        commandsToInsert.Add(PipelineCommand.SkillRound(SkillRoundCommandName, triageResult.Lead, 1));

        foreach (var participant in triageResult.Participants
                     .Where(p => p != triageResult.Lead))
        {
            commandsToInsert.Add(PipelineCommand.SkillRound(SkillRoundCommandName, participant, 1));
        }

        commandsToInsert.Add(PipelineCommand.Simple(CommandNames.ConvergenceCheck));

        Logger.LogInformation(
            "Triage complete. Lead: {Lead}, Participants: {Participants}",
            triageResult.Lead,
            string.Join(", ", triageResult.Participants));

        foreach (var (role, reason) in triageResult.Reasoning)
        {
            var included = triageResult.Participants.Contains(role) ? "INCLUDED" : "EXCLUDED";
            Logger.LogInformation("  [{Status}] {Role}: {Reason}", included, role, reason);
        }

        return CommandResult.OkAndContinueWith(
            $"Triage complete. Lead: {triageResult.Lead}. " +
            $"Participants: {string.Join(", ", triageResult.Participants)}",
            commandsToInsert.ToArray());
    }

    private TriageResult? ParseTriageResponse(
        string response, IReadOnlyList<RoleSkillDefinition> availableRoles)
    {
        try
        {
            var json = response;
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}');
            if (jsonStart >= 0 && jsonEnd > jsonStart)
                json = response[jsonStart..(jsonEnd + 1)];

            var parsed = JsonSerializer.Deserialize<TriageResult>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (parsed is null) return null;

            var validNames = availableRoles.Select(r => r.Name).ToHashSet();
            var validParticipants = parsed.Participants
                .Where(p => validNames.Contains(p))
                .ToList();

            if (validParticipants.Count == 0) return null;

            var lead = validNames.Contains(parsed.Lead) ? parsed.Lead : validParticipants[0];

            return new TriageResult { Lead = lead, Participants = validParticipants };
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to parse triage response");
            return null;
        }
    }

    private sealed class TriageResult
    {
        public string Lead { get; set; } = string.Empty;
        public List<string> Participants { get; set; } = [];
        public Dictionary<string, string> Reasoning { get; set; } = new();
    }
}
