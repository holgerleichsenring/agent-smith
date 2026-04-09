using System.Text.Json;
using System.Text.RegularExpressions;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Shared skill round logic: role lookup, LLM call, discussion log, objection handling.
/// Subclasses provide the domain-specific user prompt section.
/// </summary>
public abstract class SkillRoundHandlerBase
{
    private static readonly Regex ObjectionPattern = new(
        @"OBJECTION\s*\[?\s*(\S+)\s*\]?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    protected abstract ILogger Logger { get; }
    protected abstract string BuildDomainSection(PipelineContext pipeline);
    protected virtual string SkillRoundCommandName => "SkillRoundCommand";

    protected async Task<CommandResult> ExecuteRoundAsync(
        string skillName, int round, PipelineContext pipeline,
        ILlmClient llmClient, CancellationToken cancellationToken)
    {
        if (!pipeline.TryGet<IReadOnlyList<RoleSkillDefinition>>(
                ContextKeys.AvailableRoles, out var roles) || roles is null)
        {
            return CommandResult.Fail("No available roles in pipeline context");
        }

        var role = roles.FirstOrDefault(r => r.Name == skillName);
        if (role is null)
            return CommandResult.Fail($"Role '{skillName}' not found");

        // Fix: set ActiveSkill to current skill name for BuildDomainSection
        pipeline.Set(ContextKeys.ActiveSkill, skillName);

        // Structured/hierarchical pipelines: single typed call per skill, no discussion
        if (role.Orchestration is not null
            && pipeline.TryGet<PipelineType>(ContextKeys.PipelineTypeName, out var pipelineType)
            && pipelineType is not PipelineType.Discussion)
        {
            return await ExecuteStructuredRoundAsync(
                skillName, role, pipeline, llmClient, cancellationToken);
        }

        pipeline.TryGet<string>(ContextKeys.ProjectContext, out var projectContext);
        pipeline.TryGet<string>(ContextKeys.DomainRules, out var domainRules);
        pipeline.TryGet<string>(ContextKeys.CodeMap, out var codeMap);

        if (!pipeline.TryGet<List<DiscussionEntry>>(
                ContextKeys.DiscussionLog, out var discussionLog) || discussionLog is null)
        {
            discussionLog = [];
        }

        var domainSection = BuildDomainSection(pipeline);
        var llmResponse = await CallLlmAsync(
            role, domainSection, projectContext, domainRules, codeMap,
            discussionLog, round, llmClient, cancellationToken);
        PipelineCostTracker.GetOrCreate(pipeline).Track(llmResponse);

        var entry = new DiscussionEntry(
            skillName, role.DisplayName, role.Emoji,
            round, llmResponse.Text);
        discussionLog.Add(entry);
        pipeline.Set(ContextKeys.DiscussionLog, discussionLog);

        Logger.LogInformation(
            "{Emoji} {DisplayName} (Round {Round}): contributed to discussion",
            role.Emoji, role.DisplayName, round);

        var objectionMatch = ObjectionPattern.Match(llmResponse.Text);
        if (objectionMatch.Success)
        {
            var targetRole = objectionMatch.Groups[1].Value.Trim();
            var validTarget = roles.Any(r => r.Name == targetRole);

            if (validTarget)
            {
                var nextRound = round + 1;
                return CommandResult.OkAndContinueWith(
                    $"{role.DisplayName} objects, requesting response from {targetRole}",
                    PipelineCommand.SkillRound(SkillRoundCommandName, targetRole, nextRound),
                    PipelineCommand.SkillRound(SkillRoundCommandName, skillName, nextRound),
                    PipelineCommand.Simple(CommandNames.ConvergenceCheck));
            }
        }

        return CommandResult.Ok($"{role.DisplayName} (Round {round}): contributed");
    }

    private async Task<CommandResult> ExecuteStructuredRoundAsync(
        string skillName, RoleSkillDefinition role, PipelineContext pipeline,
        ILlmClient llmClient, CancellationToken cancellationToken)
    {
        var orch = role.Orchestration!;
        var domainSection = BuildDomainSection(pipeline);

        // Collect upstream outputs for gate/lead/executor roles
        if (!pipeline.TryGet<Dictionary<string, string>>(
                ContextKeys.SkillOutputs, out var skillOutputs) || skillOutputs is null)
        {
            skillOutputs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var upstreamContext = orch.Role switch
        {
            SkillRole.Gate => BuildGateInput(skillOutputs),
            SkillRole.Lead => BuildLeadInput(skillOutputs),
            SkillRole.Executor => BuildExecutorInput(pipeline, skillOutputs),
            _ => "" // Contributors get domain section only
        };

        var outputInstruction = orch.Role switch
        {
            SkillRole.Contributor => "Respond with a JSON array of findings. Each finding: { \"file\": \"\", \"line\": 0, \"title\": \"\", \"severity\": \"\", \"details\": \"\" }. Max 50 items.",
            SkillRole.Gate when orch.Output == SkillOutputType.List =>
                "Review all findings. Respond with JSON: { \"confirmed\": [...], \"rejected\": [...] }. Each item: { \"file\": \"\", \"line\": 0, \"title\": \"\", \"severity\": \"\", \"reason\": \"\" }.",
            SkillRole.Gate when orch.Output == SkillOutputType.Verdict =>
                "Review the analysis. Respond with JSON: { \"pass\": true/false, \"reason\": \"\" }.",
            SkillRole.Lead =>
                "Synthesize the findings into a structured assessment. Respond with a clear, numbered summary.",
            SkillRole.Executor =>
                "Based on the plan/assessment, produce your output.",
            _ => "Respond concisely."
        };

        var systemPrompt = $"""
            ## Your Role
            {role.DisplayName}: {role.Description}

            ## Role-Specific Rules
            {role.Rules}
            """;

        var userPrompt = $"""
            {domainSection}

            {(string.IsNullOrEmpty(upstreamContext) ? "" : $"## Upstream Analysis\n{upstreamContext}\n")}

            ## Output Format
            {outputInstruction}
            """;

        var llmResponse = await llmClient.CompleteAsync(
            systemPrompt, userPrompt, TaskType.Planning, cancellationToken);
        PipelineCostTracker.GetOrCreate(pipeline).Track(llmResponse);

        Logger.LogInformation(
            "{Emoji} {DisplayName} [{Role}]: structured round complete",
            role.Emoji, role.DisplayName, orch.Role);

        // Store output
        skillOutputs[skillName] = llmResponse.Text;
        pipeline.Set(ContextKeys.SkillOutputs, skillOutputs);

        // Handle gate veto
        if (orch.Role == SkillRole.Gate)
        {
            return HandleGateOutput(role, orch, llmResponse.Text);
        }

        // Lead stores plan in context for downstream
        if (orch.Role == SkillRole.Lead)
        {
            pipeline.Set(ContextKeys.ConsolidatedPlan, llmResponse.Text);
        }

        return CommandResult.Ok($"{role.DisplayName} [{orch.Role}]: complete");
    }

    private static string BuildGateInput(Dictionary<string, string> skillOutputs)
    {
        if (skillOutputs.Count == 0) return "No upstream findings.";
        return string.Join("\n\n---\n\n",
            skillOutputs.Select(kv => $"### {kv.Key}\n{kv.Value}"));
    }

    private static string BuildLeadInput(Dictionary<string, string> skillOutputs)
    {
        if (skillOutputs.Count == 0) return "No upstream findings.";
        return string.Join("\n\n---\n\n",
            skillOutputs.Select(kv => $"### {kv.Key}\n{kv.Value}"));
    }

    private static string BuildExecutorInput(PipelineContext pipeline, Dictionary<string, string> skillOutputs)
    {
        pipeline.TryGet<string>(ContextKeys.ConsolidatedPlan, out var plan);
        var sb = new System.Text.StringBuilder();
        if (!string.IsNullOrEmpty(plan))
            sb.AppendLine($"## Plan\n{plan}\n");
        if (skillOutputs.Count > 0)
            sb.AppendLine($"## Prior Outputs\n{BuildGateInput(skillOutputs)}");
        return sb.ToString();
    }

    private static CommandResult HandleGateOutput(
        RoleSkillDefinition role, SkillOrchestration orch, string responseText)
    {
        if (orch.Output == SkillOutputType.Verdict)
        {
            // Parse verdict JSON: { "pass": true/false, "reason": "..." }
            try
            {
                var json = ExtractJson(responseText);
                using var doc = JsonDocument.Parse(json);
                var pass = doc.RootElement.GetProperty("pass").GetBoolean();
                var reason = doc.RootElement.TryGetProperty("reason", out var r) ? r.GetString() : "";
                if (!pass)
                    return CommandResult.Fail($"Gate veto ({role.DisplayName}): {reason}");
            }
            catch
            {
                // If we can't parse, assume pass
            }
            return CommandResult.Ok($"Gate {role.DisplayName}: passed");
        }

        // List gate: empty confirmed list = veto
        try
        {
            var json = ExtractJson(responseText);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("confirmed", out var confirmed))
            {
                var count = confirmed.GetArrayLength();
                if (count == 0)
                    return CommandResult.Fail($"Gate veto ({role.DisplayName}): no findings confirmed");
                return CommandResult.Ok($"Gate {role.DisplayName}: {count} findings confirmed");
            }
        }
        catch
        {
            // If we can't parse, assume pass
        }

        return CommandResult.Ok($"Gate {role.DisplayName}: passed");
    }

    private static string ExtractJson(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start >= 0 && end > start)
            return text[start..(end + 1)];
        return text;
    }

    private static async Task<LlmResponse> CallLlmAsync(
        RoleSkillDefinition role,
        string domainSection,
        string? projectContext,
        string? domainRules,
        string? codeMap,
        List<DiscussionEntry> discussionLog,
        int round,
        ILlmClient llmClient,
        CancellationToken cancellationToken)
    {
        var discussionSoFar = discussionLog.Count > 0
            ? string.Join("\n\n---\n\n", discussionLog.Select(e =>
                $"{e.Emoji} {e.DisplayName} (Round {e.Round}):\n{e.Content}"))
            : "No prior discussion.";

        var systemPrompt = $"""
            ## Your Role
            {role.DisplayName}: {role.Description}

            ## Role-Specific Rules
            {role.Rules}
            """;

        var userPrompt = $"""
            {domainSection}

            ## Project Context
            {projectContext ?? "Not available"}

            ## Domain Rules
            {domainRules ?? "Not available"}

            ## Code Map
            {codeMap ?? "Not available"}

            ## Discussion So Far
            {discussionSoFar}

            ## Your Task
            Based on the discussion so far, provide your analysis.
            This is round {round}.

            If this is the first round for the lead role: Create an initial analysis.
            If responding to an existing analysis: Review it from your perspective.

            At the end of your response, state clearly:
            - AGREE: if you accept the current analysis
            - OBJECTION [target_role]: if you have a blocking concern for a specific role
            - SUGGESTION: if you have a non-blocking improvement

            Keep your contribution focused and concise (max 500 words).
            """;

        return await llmClient.CompleteAsync(
            systemPrompt, userPrompt, TaskType.Planning, cancellationToken);
    }
}
