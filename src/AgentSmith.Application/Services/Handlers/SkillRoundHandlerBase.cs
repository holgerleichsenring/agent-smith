using System.Text.RegularExpressions;
using AgentSmith.Contracts.Commands;
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
    IGateOutputHandler gateOutputHandler,
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

        if (!pipeline.TryGet<List<DiscussionEntry>>(
                ContextKeys.DiscussionLog, out var discussionLog) || discussionLog is null)
            discussionLog = [];

        var domainSection = BuildDomainSection(pipeline);
        var (systemPrompt, userPrompt) = promptBuilder.BuildDiscussionPrompt(
            role, domainSection, projectContext, domainRules, codeMap, discussionLog, round);

        var llmResponse = await llmClient.CompleteAsync(
            systemPrompt, userPrompt, TaskType.Planning, cancellationToken);
        PipelineCostTracker.GetOrCreate(pipeline).Track(llmResponse);

        discussionLog.Add(new DiscussionEntry(
            skillName, role.DisplayName, role.Emoji, round, llmResponse.Text));
        pipeline.Set(ContextKeys.DiscussionLog, discussionLog);

        Logger.LogInformation("{Emoji} {DisplayName} (Round {Round}): contributed",
            role.Emoji, role.DisplayName, round);

        return DetectObjection(llmResponse.Text, role, roles, round)
            ?? CommandResult.Ok($"{role.DisplayName} (Round {round}): contributed");
    }

    private async Task<CommandResult> ExecuteStructuredRoundAsync(
        string skillName, RoleSkillDefinition role, PipelineContext pipeline,
        ILlmClient llmClient, CancellationToken cancellationToken)
    {
        var orch = role.Orchestration!;

        if (!pipeline.TryGet<Dictionary<string, string>>(
                ContextKeys.SkillOutputs, out var skillOutputs) || skillOutputs is null)
            skillOutputs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var domainSection = BuildDomainSection(pipeline);
        var upstreamContext = upstreamContextBuilder.Build(orch.Role, pipeline, skillOutputs);
        var outputInstruction = _instructionBuilder.Build(orch);

        var (systemPrompt, userPrompt) = promptBuilder.BuildStructuredPrompt(
            role, domainSection, upstreamContext, outputInstruction);

        var llmResponse = await llmClient.CompleteAsync(
            systemPrompt, userPrompt, TaskType.Planning, cancellationToken);
        PipelineCostTracker.GetOrCreate(pipeline).Track(llmResponse);

        Logger.LogInformation("{Emoji} {DisplayName} [{Role}]: structured round complete",
            role.Emoji, role.DisplayName, orch.Role);

        skillOutputs[skillName] = llmResponse.Text;
        pipeline.Set(ContextKeys.SkillOutputs, skillOutputs);

        if (orch.Role == SkillRole.Gate)
            return gateOutputHandler.Handle(role, orch, llmResponse.Text, pipeline);

        if (orch.Role == SkillRole.Lead)
            pipeline.Set(ContextKeys.ConsolidatedPlan, llmResponse.Text);

        return CommandResult.Ok($"{role.DisplayName} [{orch.Role}]: complete");
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
