using System.Diagnostics;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Models;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.Models;
using GenerativeAI;
using GenerativeAI.Types;
using Microsoft.Extensions.Logging;
using TaskType = AgentSmith.Contracts.Providers.TaskType;

namespace AgentSmith.Infrastructure.Services.Providers.Agent;

/// <summary>
/// AI agent provider using the Google Gemini API.
/// Supports Gemini 2.5 Flash, Gemini 2.5 Pro, and other Gemini models with function calling.
/// </summary>
public sealed class GeminiAgentProvider(
    string apiKey,
    string model,
    IModelRegistry? modelRegistry,
    PricingConfig pricingConfig,
    ILogger<GeminiAgentProvider> logger,
    AgentPromptBuilder promptBuilder) : IAgentProvider
{
    public string ProviderType => "Gemini";

    public Task<Plan> GeneratePlanAsync(
        Ticket ticket,
        ProjectMap projectMap,
        string codingPrinciples,
        string? codeMap,
        string? projectContext,
        IReadOnlyList<TicketImageAttachment>? images,
        CancellationToken cancellationToken)
    {
        if (images is { Count: > 0 })
            logger.LogWarning("Gemini vision for ticket images not yet implemented, {Count} image(s) ignored", images.Count);
        return GeneratePlanCoreAsync(ticket, projectMap, codingPrinciples, codeMap, projectContext, cancellationToken);
    }

    private async Task<Plan> GeneratePlanCoreAsync(
        Ticket ticket,
        ProjectMap projectMap,
        string codingPrinciples,
        string? codeMap,
        string? projectContext,
        CancellationToken cancellationToken)
    {
        var planModel = ResolveModel(TaskType.Planning);
        var genModel = CreateModel(planModel.Model);

        var systemPrompt = promptBuilder.BuildPlanSystemPrompt(codingPrinciples, codeMap, projectContext);
        var userPrompt = promptBuilder.BuildPlanUserPrompt(ticket, projectMap);

        var response = await genModel.GenerateContentAsync(
            new GenerateContentRequest
            {
                SystemInstruction = new Content(systemPrompt, Roles.User),
                Contents = new List<Content>
                {
                    new(userPrompt, Roles.User)
                }
            },
            cancellationToken: cancellationToken);

        var candidate = response?.Candidates?.FirstOrDefault();
        var rawResponse = candidate?.Content?.Parts?
            .Where(p => p.Text is not null)
            .Select(p => p.Text!)
            .FirstOrDefault()
            ?? throw new ProviderException("Gemini", "Gemini returned no text response.");

        return PlanParser.Parse("Gemini", rawResponse);
    }

    public async Task<AgentExecutionResult> ExecutePlanAsync(
        Plan plan,
        Repository repository,
        string codingPrinciples,
        string? codeMap,
        string? projectContext,
        IProgressReporter progressReporter,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var tracker = new TokenUsageTracker();
        var costTracker = CreateCostTracker(tracker);

        tracker.SetPhase("primary");
        var primaryModel = ResolveModel(TaskType.Primary);
        costTracker?.SetPhaseModel("primary", primaryModel.Model);

        var fileReadTracker = new FileReadTracker();
        var toolExecutor = new ToolExecutor(
            repository.LocalPath, logger, fileReadTracker, progressReporter);
        var genModel = CreateModel(primaryModel.Model);

        var loop = new GeminiAgenticLoop(
            genModel, toolExecutor, logger, tracker, progressReporter, 25);

        var systemPrompt = promptBuilder.BuildExecutionSystemPrompt(codingPrinciples, codeMap, projectContext);
        var userMessage = promptBuilder.BuildExecutionUserPrompt(plan, repository);

        var changes = await loop.RunAsync(systemPrompt, userMessage, cancellationToken);
        var decisions = toolExecutor.GetDecisions();
        sw.Stop();

        logger.LogInformation(
            "Gemini agentic execution completed with {Count} file changes and {Decisions} decisions in {Seconds}s",
            changes.Count, decisions.Count, (int)sw.Elapsed.TotalSeconds);

        var costSummary = LogCostSummary(costTracker, tracker);

        return new AgentExecutionResult(
            changes, costSummary, (int)sw.Elapsed.TotalSeconds, decisions);
    }

    private ModelAssignment ResolveModel(TaskType taskType)
    {
        if (modelRegistry is not null)
            return modelRegistry.GetModel(taskType);

        return new ModelAssignment { Model = model, MaxTokens = AgentDefaults.DefaultMaxTokens };
    }

    private GenerativeModel CreateModel(string modelId)
    {
        var googleAi = new GoogleAi(apiKey);
        return googleAi.CreateGenerativeModel(modelId);
    }

    private CostTracker? CreateCostTracker(TokenUsageTracker tracker)
    {
        if (pricingConfig.Models.Count == 0)
            return null;

        var costTracker = new CostTracker(pricingConfig, logger);
        var planningModel = ResolveModel(TaskType.Planning);
        costTracker.SetPhaseModel("planning", planningModel.Model);
        return costTracker;
    }

    private RunCostSummary? LogCostSummary(CostTracker? costTracker, TokenUsageTracker tracker)
    {
        tracker.LogSummary(logger);
        if (costTracker is null) return null;

        var costSummary = costTracker.CalculateCost(tracker);
        costTracker.LogCostSummary(costSummary);
        return costSummary;
    }
}
