using AgentSmith.Application.Extensions;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Application.Services.Prompts;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Generates an execution plan via M.E.AI IChatClient. Builds prompts via the
/// shared AgentPromptBuilder, calls TaskType.Planning, parses with PlanParser.
/// Writes plan decisions to the decision log after parsing.
/// </summary>
public sealed class GeneratePlanHandler(
    IChatClientFactory chatClientFactory,
    AgentPromptBuilder promptBuilder,
    IDecisionLogger decisionLogger,
    ILogger<GeneratePlanHandler> logger)
    : ICommandHandler<GeneratePlanContext>
{
    public async Task<CommandResult> ExecuteAsync(
        GeneratePlanContext context, CancellationToken cancellationToken)
    {
        if (context.Pipeline.TryGet<PipelineType>(ContextKeys.PipelineTypeName, out var pipelineType)
            && pipelineType is PipelineType.Discussion or PipelineType.Structured)
        {
            logger.LogInformation("Skipping plan generation — pipeline type is {Type}", pipelineType);
            return CommandResult.Ok($"Plan generation skipped for {pipelineType} pipeline");
        }

        var projectContext = MergeContext(context);

        logger.LogInformation("Generating plan for ticket {Ticket}...", context.Ticket.Id);

        var systemPrompt = promptBuilder.BuildPlanSystemPrompt(
            context.CodingPrinciples, context.CodeMap, projectContext);
        var userPrompt = promptBuilder.BuildPlanUserPrompt(context.Ticket, context.ProjectMap);

        var chat = chatClientFactory.Create(context.AgentConfig, TaskType.Planning);
        var maxTokens = chatClientFactory.GetMaxOutputTokens(context.AgentConfig, TaskType.Planning);
        var model = chatClientFactory.GetModel(context.AgentConfig, TaskType.Planning);
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User, userPrompt),
        };
        var response = await chat.GetResponseAsync(messages,
            new ChatOptions { MaxOutputTokens = maxTokens }, cancellationToken);
        PipelineCostTracker.GetOrCreate(context.Pipeline).Track(response);

        var plan = PlanParser.Parse(model, response.Text ?? string.Empty);
        context.Pipeline.Set(ContextKeys.Plan, plan);

        if (plan.Decisions.Count > 0)
        {
            context.Pipeline.TryGet<Repository>(ContextKeys.Repository, out var repo);
            var sourceLabel = $"#{context.Ticket.Id}";
            await WriteDecisionsAsync(repo?.LocalPath, plan.Decisions, sourceLabel, cancellationToken);
            context.Pipeline.AppendDecisions(plan.Decisions);
        }

        logger.LogInformation(
            "Plan generated: {Summary} ({Steps} steps, {Decisions} decisions)",
            plan.Summary, plan.Steps.Count, plan.Decisions.Count);

        return CommandResult.Ok($"Plan generated with {plan.Steps.Count} steps");
    }

    private string MergeContext(GeneratePlanContext context)
    {
        var projectContext = context.ProjectContext;

        if (context.Pipeline.TryGet<ConvergenceResult>(
                ContextKeys.ConvergenceResult, out var convergenceResult)
            && convergenceResult is not null)
        {
            logger.LogInformation(
                "Using ConvergenceResult: {Blocking} blocking, {NonBlocking} non-blocking observations",
                convergenceResult.Blocking.Count, convergenceResult.NonBlocking.Count);

            var structuredInput = BuildStructuredInput(convergenceResult);
            return string.IsNullOrEmpty(projectContext) ? structuredInput : $"{projectContext}\n\n{structuredInput}";
        }

        if (context.Pipeline.TryGet<string>(ContextKeys.ConsolidatedPlan, out var consolidated)
            && consolidated is not null)
        {
            logger.LogInformation("Including consolidated multi-role discussion as plan context");
            return string.IsNullOrEmpty(projectContext)
                ? $"## Multi-Role Discussion\n\n{consolidated}"
                : $"{projectContext}\n\n## Multi-Role Discussion\n\n{consolidated}";
        }

        return projectContext ?? string.Empty;
    }

    private static string BuildStructuredInput(ConvergenceResult convergenceResult)
    {
        var sections = new List<string> { "## Multi-Role Analysis (Structured)" };

        if (convergenceResult.Blocking.Count > 0)
        {
            sections.Add("### Blocking Observations — each MUST map to a plan step");
            foreach (var obs in convergenceResult.Blocking)
            {
                var effort = obs.Effort.HasValue ? $" | effort: {obs.Effort}" : "";
                var loc = obs.DisplayLocation;
                var location = loc != "General" ? $" | target: {loc}" : "";
                sections.Add(
                    $"- [{obs.Id}] **{obs.Concern}** ({obs.Severity}, confidence: {obs.Confidence}){effort}{location}\n" +
                    $"  {obs.Description}\n" +
                    (string.IsNullOrWhiteSpace(obs.Suggestion) ? "" : $"  → Action: {obs.Suggestion}"));
            }
        }

        if (convergenceResult.NonBlocking.Count > 0)
        {
            sections.Add("### Non-Blocking Observations — address if feasible");
            foreach (var obs in convergenceResult.NonBlocking)
            {
                sections.Add(
                    $"- [{obs.Id}] **{obs.Concern}** ({obs.Severity}): {obs.Description}" +
                    (string.IsNullOrWhiteSpace(obs.Suggestion) ? "" : $" → {obs.Suggestion}"));
            }
        }

        if (convergenceResult.Links.Count > 0)
        {
            sections.Add("### Observation Relationships");
            foreach (var link in convergenceResult.Links)
            {
                sections.Add($"- [{link.ObservationId}] {link.Relationship} [{link.RelatedObservationId}]");
            }
        }

        return string.Join("\n\n", sections);
    }

    private async Task WriteDecisionsAsync(
        string? repoPath, IReadOnlyList<PlanDecision> decisions,
        string? sourceLabel, CancellationToken cancellationToken)
    {
        foreach (var d in decisions)
        {
            if (Enum.TryParse<DecisionCategory>(d.Category, true, out var cat))
                await decisionLogger.LogAsync(repoPath, cat, d.Decision, cancellationToken, sourceLabel);
            else
                logger.LogWarning("Unknown decision category '{Category}', skipping", d.Category);
        }
    }
}
