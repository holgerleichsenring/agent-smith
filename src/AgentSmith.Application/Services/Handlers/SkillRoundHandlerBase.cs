using System.Text.RegularExpressions;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Orchestrates a single skill round — delegates prompt building, LLM calls,
/// gate handling, and upstream context to injected services.
/// Subclasses provide only the domain-specific context section.
/// </summary>
public abstract class SkillRoundHandlerBase(
    ISkillPromptBuilder promptBuilder,
    IGateRetryCoordinator gateRetryCoordinator,
    IUpstreamContextBuilder upstreamContextBuilder)
{
    private static readonly Regex ObjectionPattern = new(
        @"OBJECTION\s*\[?\s*(\S+)\s*\]?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly StructuredOutputInstructionBuilder _instructionBuilder = new();

    protected abstract ILogger Logger { get; }
    protected abstract string BuildDomainSection(PipelineContext pipeline);
    protected virtual string SkillRoundCommandName => "SkillRoundCommand";

    protected async Task<CommandResult> ExecuteRoundAsync(
        string skillName, int round, PipelineContext pipeline,
        ILlmClient llmClient, CancellationToken cancellationToken)
    {
        if (!pipeline.TryGet<IReadOnlyList<RoleSkillDefinition>>(
                ContextKeys.AvailableRoles, out var roles) || roles is null)
            return CommandResult.Fail("No available roles in pipeline context");

        var role = roles.FirstOrDefault(r => r.Name == skillName);
        if (role is null)
            return CommandResult.Fail($"Role '{skillName}' not found");

        pipeline.Set(ContextKeys.ActiveSkill, skillName);

        if (IsStructuredRound(role, pipeline))
            return await ExecuteStructuredRoundAsync(
                skillName, role, pipeline, llmClient, cancellationToken);

        return await ExecuteDiscussionRoundAsync(
            skillName, role, roles, round, pipeline, llmClient, cancellationToken);
    }

    private async Task<CommandResult> ExecuteDiscussionRoundAsync(
        string skillName, RoleSkillDefinition role,
        IReadOnlyList<RoleSkillDefinition> roles, int round,
        PipelineContext pipeline, ILlmClient llmClient,
        CancellationToken cancellationToken)
    {
        pipeline.TryGet<string>(ContextKeys.ProjectContext, out var projectContext);
        pipeline.TryGet<string>(ContextKeys.DomainRules, out var domainRules);
        pipeline.TryGet<string>(ContextKeys.CodeMap, out var codeMap);

        pipeline.TryGet<List<DiscussionEntry>>(ContextKeys.DiscussionLog, out var discussionLog);
        var discussionForPrompt = (IReadOnlyList<DiscussionEntry>)(discussionLog ?? []);

        var domainSection = BuildDomainSection(pipeline);
        var (systemPrompt, userPrompt) = promptBuilder.BuildDiscussionPrompt(
            role, domainSection, projectContext, domainRules, codeMap, discussionForPrompt, round);

        var llmResponse = await llmClient.CompleteAsync(
            systemPrompt, userPrompt, TaskType.Planning, cancellationToken);
        PipelineCostTracker.GetOrCreate(pipeline).Track(llmResponse);

        var parsed = ObservationParser.ParseWithoutIds(llmResponse.Text, skillName, Logger);
        var renderedText = RenderObservationsAsText(parsed);
        var discussionEntry = new DiscussionEntry(
            skillName, role.DisplayName, role.Emoji, round, renderedText);

        var buffer = new SkillRoundBuffer(skillName, round, parsed, discussionEntry, null);
        DispatchBuffer(pipeline, buffer);

        Logger.LogInformation(
            "{Emoji} {DisplayName} (Round {Round}): {Count} observations",
            role.Emoji, role.DisplayName, round, parsed.Count);

        return DetectBlockingFollowUp(parsed, skillName, role, roles, round)
            ?? DetectObjection(renderedText, role, roles, round)
            ?? CommandResult.Ok($"{role.DisplayName} (Round {round}): {parsed.Count} observations");
    }

    private CommandResult? DetectBlockingFollowUp(
        IReadOnlyList<SkillObservation> parsed, string skillName,
        RoleSkillDefinition role, IReadOnlyList<RoleSkillDefinition> roles, int round)
    {
        var blocking = parsed.FirstOrDefault(o => o.Blocking);
        if (blocking is null) return null;

        var targetRole = roles.FirstOrDefault(r =>
            r.Name != skillName &&
            r.Triggers.Any(t => t.Contains(
                blocking.Concern.ToString(), StringComparison.OrdinalIgnoreCase)));
        if (targetRole is null) return null;

        var nextRound = round + 1;
        return CommandResult.OkAndContinueWith(
            $"{role.DisplayName} has blocking concern ({blocking.Concern}), requesting {targetRole.DisplayName}",
            PipelineCommand.SkillRound(SkillRoundCommandName, targetRole.Name, nextRound),
            PipelineCommand.SkillRound(SkillRoundCommandName, skillName, nextRound),
            PipelineCommand.Simple(CommandNames.ConvergenceCheck));
    }

    private static void DispatchBuffer(PipelineContext pipeline, SkillRoundBuffer buffer)
    {
        if (pipeline.TryGet<List<SkillRoundBuffer>>(
                ContextKeys.DeferredBuffers, out var deferred) && deferred is not null)
        {
            lock (deferred) deferred.Add(buffer);
            return;
        }

        ApplyBufferToContext(pipeline, buffer);
    }

    /// <summary>
    /// Merges a buffer into the shared pipeline context. Assigns sequential observation IDs
    /// based on the current observation list state, then appends observations, the discussion
    /// entry, and any structured output. Caller is responsible for ordering when invoked
    /// against the same pipeline from multiple buffers.
    /// </summary>
    internal static void ApplyBufferToContext(PipelineContext pipeline, SkillRoundBuffer buffer)
    {
        if (buffer.Observations.Count > 0)
            ApplyObservations(pipeline, buffer.Observations);

        if (buffer.DiscussionEntry is not null)
            ApplyDiscussionEntry(pipeline, buffer.DiscussionEntry);

        if (buffer.StructuredOutput is not null)
            ApplyStructuredOutput(pipeline, buffer.SkillName, buffer.StructuredOutput);
    }

    private static void ApplyObservations(
        PipelineContext pipeline, IReadOnlyList<SkillObservation> parsed)
    {
        if (!pipeline.TryGet<List<SkillObservation>>(
                ContextKeys.SkillObservations, out var observations) || observations is null)
            observations = [];

        var nextId = observations.Count > 0 ? observations.Max(o => o.Id) + 1 : 1;
        foreach (var obs in parsed)
        {
            observations.Add(obs with { Id = nextId++ });
        }
        pipeline.Set(ContextKeys.SkillObservations, observations);
    }

    private static void ApplyDiscussionEntry(PipelineContext pipeline, DiscussionEntry entry)
    {
        if (!pipeline.TryGet<List<DiscussionEntry>>(
                ContextKeys.DiscussionLog, out var log) || log is null)
            log = [];
        log.Add(entry);
        pipeline.Set(ContextKeys.DiscussionLog, log);
    }

    private static void ApplyStructuredOutput(
        PipelineContext pipeline, string skillName, string output)
    {
        if (!pipeline.TryGet<Dictionary<string, string>>(
                ContextKeys.SkillOutputs, out var outputs) || outputs is null)
            outputs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        outputs[skillName] = output;
        pipeline.Set(ContextKeys.SkillOutputs, outputs);
    }

    private static string RenderObservationsAsText(List<SkillObservation> observations)
    {
        if (observations.Count == 0) return "No observations.";

        return string.Join("\n\n", observations.Select(o =>
        {
            var blocking = o.Blocking ? " [BLOCKING]" : "";
            var location = !string.IsNullOrWhiteSpace(o.Location) ? $" ({o.Location})" : "";
            return $"**{o.Concern}{blocking}** [{o.Severity}] (confidence: {o.Confidence}){location}\n" +
                   $"{o.Description}\n" +
                   (string.IsNullOrWhiteSpace(o.Suggestion) ? "" : $"→ {o.Suggestion}");
        }));
    }

    private async Task<CommandResult> ExecuteStructuredRoundAsync(
        string skillName, RoleSkillDefinition role, PipelineContext pipeline,
        ILlmClient llmClient, CancellationToken cancellationToken)
    {
        var orch = role.Orchestration!;

        pipeline.TryGet<Dictionary<string, string>>(
            ContextKeys.SkillOutputs, out var skillOutputs);
        var upstreamSnapshot = skillOutputs is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(skillOutputs, StringComparer.OrdinalIgnoreCase);

        var domainSection = BuildDomainSection(pipeline);
        var upstreamContext = upstreamContextBuilder.Build(orch.Role, pipeline, upstreamSnapshot);
        var outputInstruction = _instructionBuilder.Build(orch);

        var (systemPrompt, userPrompt) = promptBuilder.BuildStructuredPrompt(
            role, domainSection, upstreamContext, outputInstruction);

        if (orch.Role == SkillRole.Gate)
            return await ExecuteGateRoundAsync(
                skillName, role, orch, systemPrompt, userPrompt, pipeline, llmClient, cancellationToken);

        var llmResponse = await llmClient.CompleteAsync(
            systemPrompt, userPrompt, TaskType.Planning, cancellationToken);
        PipelineCostTracker.GetOrCreate(pipeline).Track(llmResponse);

        Logger.LogInformation("{Emoji} {DisplayName} [{Role}]: structured round complete",
            role.Emoji, role.DisplayName, orch.Role);

        var buffer = new SkillRoundBuffer(skillName, 0, [], null, llmResponse.Text);
        DispatchBuffer(pipeline, buffer);

        if (orch.Role == SkillRole.Lead)
            pipeline.Set(ContextKeys.ConsolidatedPlan, llmResponse.Text);

        return CommandResult.Ok($"{role.DisplayName} [{orch.Role}]: complete");
    }

    private async Task<CommandResult> ExecuteGateRoundAsync(
        string skillName, RoleSkillDefinition role, SkillOrchestration orch,
        string systemPrompt, string userPrompt, PipelineContext pipeline,
        ILlmClient llmClient, CancellationToken cancellationToken)
    {
        var outcome = await gateRetryCoordinator.ExecuteAsync(
            role, orch, systemPrompt, userPrompt, llmClient, pipeline, cancellationToken);

        var buffer = new SkillRoundBuffer(skillName, 0, [], null, outcome.FinalResponseText);
        DispatchBuffer(pipeline, buffer);

        Logger.LogInformation("{Emoji} {DisplayName} [Gate]: {Message}",
            role.Emoji, role.DisplayName, outcome.Result.Message);

        return outcome.Result;
    }

    private CommandResult? DetectObjection(
        string responseText, RoleSkillDefinition role,
        IReadOnlyList<RoleSkillDefinition> roles, int round)
    {
        var match = ObjectionPattern.Match(responseText);
        if (!match.Success) return null;

        var targetRole = match.Groups[1].Value.Trim();
        if (!roles.Any(r => r.Name == targetRole)) return null;

        var nextRound = round + 1;
        return CommandResult.OkAndContinueWith(
            $"{role.DisplayName} objects, requesting response from {targetRole}",
            PipelineCommand.SkillRound(SkillRoundCommandName, targetRole, nextRound),
            PipelineCommand.SkillRound(SkillRoundCommandName, role.Name, nextRound),
            PipelineCommand.Simple(CommandNames.ConvergenceCheck));
    }

    private static bool IsStructuredRound(RoleSkillDefinition role, PipelineContext pipeline) =>
        role.Orchestration is not null
        && pipeline.TryGet<PipelineType>(ContextKeys.PipelineTypeName, out var pipelineType)
        && pipelineType is not PipelineType.Discussion;
}
