using AgentSmith.Infrastructure.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.Models;
using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Agent;

/// <summary>
/// AI agent provider using the Anthropic Claude API.
/// Supports plan generation (single call) and agentic execution (tool-calling loop).
/// </summary>
public sealed class ClaudeAgentProvider(
    string apiKey,
    string model,
    RetryConfig retryConfig,
    CacheConfig cacheConfig,
    CompactionConfig compactionConfig,
    IModelRegistry? modelRegistry,
    PricingConfig pricingConfig,
    ILogger<ClaudeAgentProvider> logger) : IAgentProvider
{
    public string ProviderType => "Claude";

    public async Task<Plan> GeneratePlanAsync(
        Ticket ticket,
        CodeAnalysis codeAnalysis,
        string codingPrinciples,
        CancellationToken cancellationToken = default)
    {
        using var client = CreateResilientClient();

        var systemPrompt = AgentPromptBuilder.BuildPlanSystemPrompt(codingPrinciples);
        var userPrompt = AgentPromptBuilder.BuildPlanUserPrompt(ticket, codeAnalysis);

        var planModel = ResolveModel(TaskType.Planning);

        var response = await client.Messages.GetClaudeMessageAsync(
            new MessageParameters
            {
                Model = planModel.Model,
                MaxTokens = planModel.MaxTokens,
                System = new List<SystemMessage> { new(systemPrompt) },
                Messages = new List<Message>
                {
                    new()
                    {
                        Role = RoleType.User,
                        Content = new List<ContentBase> { new TextContent { Text = userPrompt } }
                    }
                },
                Stream = false,
                PromptCaching = ResolveCacheType()
            },
            cancellationToken);

        var rawResponse = ExtractTextResponse(response);
        return PlanParser.Parse("Claude", rawResponse);
    }

    public async Task<IReadOnlyList<CodeChange>> ExecutePlanAsync(
        Plan plan,
        Repository repository,
        string codingPrinciples,
        IProgressReporter? progressReporter = null,
        CancellationToken cancellationToken = default)
    {
        using var client = CreateResilientClient();

        var tracker = new TokenUsageTracker();
        var costTracker = CreateCostTracker(tracker);

        var scoutResult = await TryRunScoutAsync(
            plan, repository.LocalPath, tracker, costTracker,
            progressReporter, cancellationToken);

        tracker.SetPhase("primary");
        var primaryModel = ResolveModel(TaskType.Primary);
        costTracker?.SetPhaseModel("primary", primaryModel.Model);

        var fileReadTracker = new FileReadTracker();
        var toolExecutor = new ToolExecutor(
            repository.LocalPath, logger, fileReadTracker, progressReporter);
        var compactor = CreateCompactor(tracker, costTracker);
        var loop = new AgenticLoop(
            client, primaryModel.Model, toolExecutor, logger,
            cacheConfig, tracker, compactionConfig, compactor, progressReporter);

        var systemPrompt = AgentPromptBuilder.BuildExecutionSystemPrompt(codingPrinciples);
        var userMessage = AgentPromptBuilder.BuildExecutionUserPrompt(
            plan, repository, scoutResult);

        var changes = await loop.RunAsync(systemPrompt, userMessage, cancellationToken);

        logger.LogInformation(
            "Agentic execution completed with {Count} file changes", changes.Count);

        LogCostSummary(costTracker, tracker);

        return changes;
    }

    private ModelAssignment ResolveModel(TaskType taskType)
    {
        if (modelRegistry is not null)
            return modelRegistry.GetModel(taskType);

        return new ModelAssignment { Model = model, MaxTokens = AgentDefaults.DefaultMaxTokens };
    }

    private async Task<ScoutResult?> TryRunScoutAsync(
        Plan plan, string repositoryPath,
        TokenUsageTracker tracker, CostTracker? costTracker,
        IProgressReporter? progressReporter,
        CancellationToken cancellationToken)
    {
        if (modelRegistry is null)
            return null;

        var scoutModel = modelRegistry.GetModel(TaskType.Scout);
        tracker.SetPhase("scout");
        costTracker?.SetPhaseModel("scout", scoutModel.Model);

        logger.LogInformation("Running scout agent with model {Model}", scoutModel.Model);
        ReportDetail(progressReporter, "\ud83d\udd0d Scout: analyzing codebase...");

        using var scoutClient = CreateResilientClient();
        var scout = new ScoutAgent(
            scoutClient, scoutModel.Model, scoutModel.MaxTokens, logger, tracker, progressReporter);
        var result = await scout.DiscoverAsync(plan, repositoryPath, cancellationToken);

        ReportDetail(progressReporter,
            $"\ud83d\udd0d Scout: found {result.RelevantFiles.Count} relevant files");

        return result;
    }

    private void ReportDetail(IProgressReporter? reporter, string text)
    {
        try { reporter?.ReportDetailAsync(text).GetAwaiter().GetResult(); }
        catch (Exception ex) { logger.LogDebug(ex, "Detail reporting failed"); }
    }

    private ClaudeContextCompactor? CreateCompactor(
        TokenUsageTracker tracker, CostTracker? costTracker)
    {
        if (!compactionConfig.IsEnabled)
            return null;

        var summaryModel = modelRegistry is not null
            ? modelRegistry.GetModel(TaskType.Summarization)
            : new ModelAssignment { Model = compactionConfig.SummaryModel, MaxTokens = AgentDefaults.CompactionMaxTokens };

        costTracker?.SetPhaseModel("compaction", summaryModel.Model);

        var compactorClient = CreateResilientClient();
        return new ClaudeContextCompactor(compactorClient, summaryModel.Model, logger, tracker);
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

    private void LogCostSummary(CostTracker? costTracker, TokenUsageTracker tracker)
    {
        tracker.LogSummary(logger);
        if (costTracker is null) return;

        var costSummary = costTracker.CalculateCost(tracker);
        costTracker.LogCostSummary(costSummary);
    }

    private PromptCacheType ResolveCacheType()
    {
        if (!cacheConfig.IsEnabled) return PromptCacheType.None;
        return cacheConfig.Strategy.ToLowerInvariant() switch
        {
            "automatic" => PromptCacheType.AutomaticToolsAndSystem,
            "fine-grained" => PromptCacheType.FineGrained,
            _ => PromptCacheType.None
        };
    }

    private AnthropicClient CreateResilientClient()
    {
        var factory = new ResilientHttpClientFactory(retryConfig, logger);
        var httpClient = factory.Create();
        return new AnthropicClient(apiKey, httpClient);
    }

    private static string ExtractTextResponse(MessageResponse response)
    {
        var textContent = response.Content.OfType<TextContent>().FirstOrDefault();
        return textContent?.Text
            ?? throw new ProviderException("Claude", "Claude returned no text response.");
    }
}
