using AgentSmith.Application.Extensions;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Prompts;
using AgentSmith.Application.Services.Validation;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Generates an execution plan via M.E.AI IChatClient. Builds prompts via
/// AgentPromptBuilder, calls TaskType.Planning, parses with PlanParser (strict
/// with legacy fallback). Decisions go to the log; the open-questions
/// side-channel is published via <see cref="IPlanOpenQuestionExtractor"/>.
/// </summary>
public sealed class GeneratePlanHandler(
    IChatClientFactory chatClientFactory,
    AgentPromptBuilder promptBuilder,
    IDecisionLogger decisionLogger,
    PlanOutputValidator planValidator,
    PlanParser planParser,
    IPlanOpenQuestionExtractor questionExtractor,
    IRunContextAccessor runContext,
    ILogger<GeneratePlanHandler> logger)
    : ICommandHandler<GeneratePlanContext>
{
    public async Task<CommandResult> ExecuteAsync(
        GeneratePlanContext context, CancellationToken cancellationToken)
    {
        if (context.Pipeline.TryGet<PipelineType>(ContextKeys.PipelineTypeName, out var type)
            && type is PipelineType.Discussion or PipelineType.Structured)
            return CommandResult.Ok($"Plan generation skipped for {type} pipeline");

        var projectContext = PlanContextRenderer.Merge(context.ProjectContext, context.Pipeline);
        var planAnswers = ResolvePlanAnswers(context.Pipeline);
        logger.LogInformation("Generating plan for ticket {Ticket}...", context.Ticket.Id);
        var system = promptBuilder.BuildPlanSystemPrompt(
            context.CodingPrinciples, context.CodeMap, projectContext);
        var user = promptBuilder.BuildPlanUserPrompt(context.Ticket, context.ProjectMap, planAnswers);

        var rawText = await CallPlannerAsync(context, system, user, cancellationToken);
        var plan = ParseWithFallback(chatClientFactory.GetModel(context.AgentConfig, TaskType.Planning), rawText);
        context.Pipeline.Set(ContextKeys.Plan, plan);

        questionExtractor.PublishSideChannel(plan, rawText, context.Pipeline);
        await WriteDecisionsAsync(context, plan.Decisions, cancellationToken);

        logger.LogInformation("Plan generated: {Summary} ({Steps} steps, {Decisions} decisions)",
            plan.Summary, plan.Steps.Count, plan.Decisions.Count);
        return CommandResult.Ok($"Plan generated with {plan.Steps.Count} steps");
    }

    private async Task<string> CallPlannerAsync(
        GeneratePlanContext context, string system, string user, CancellationToken cancellationToken)
    {
        var chat = chatClientFactory.Create(context.AgentConfig, TaskType.Planning);
        var maxTokens = chatClientFactory.GetMaxOutputTokens(context.AgentConfig, TaskType.Planning);
        using var _scope = runContext.BeginCallScope("planner", SkillExecutionPhase.Plan.ToString());
        var response = await chat.GetResponseAsync(
            [new(ChatRole.System, system), new(ChatRole.User, user)],
            new ChatOptions { MaxOutputTokens = maxTokens }, cancellationToken);
        PipelineCostTracker.GetOrCreate(context.Pipeline).Track(response);
        return response.Text ?? string.Empty;
    }

    private Plan ParseWithFallback(string model, string rawText)
    {
        var strict = planParser.ParseStrict(rawText, planValidator);
        if (strict.Plan is not null) return strict.Plan;
        logger.LogDebug("Strict parse rejected ({Reason}); falling back to legacy", strict.Validation.ErrorMessage);
        return planParser.Parse(model, rawText);
    }

    private static IReadOnlyDictionary<string, string>? ResolvePlanAnswers(PipelineContext pipeline) =>
        pipeline.TryGet<Dictionary<string, string>>(ContextKeys.PlanAnswers, out var answers)
        && answers is { Count: > 0 } ? answers : null;

    private async Task WriteDecisionsAsync(
        GeneratePlanContext context, IReadOnlyList<PlanDecision> decisions, CancellationToken cancellationToken)
    {
        if (decisions.Count == 0) return;
        context.Pipeline.TryGet<Repository>(ContextKeys.Repository, out var repo);
        var sourceLabel = $"#{context.Ticket.Id}";
        foreach (var d in decisions)
            if (Enum.TryParse<DecisionCategory>(d.Category, true, out var cat))
                await decisionLogger.LogAsync(repo?.LocalPath, cat, d.Decision, cancellationToken, sourceLabel);
            else
                logger.LogWarning("Unknown decision category '{Category}', skipping", d.Category);
        context.Pipeline.AppendDecisions(decisions);
    }
}
