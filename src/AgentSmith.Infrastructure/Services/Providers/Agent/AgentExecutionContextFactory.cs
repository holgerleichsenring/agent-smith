using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Models;
using Anthropic.SDK;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Agent;

/// <summary>
/// Creates the infrastructure objects (trackers, compactor, cost tracking)
/// needed by <see cref="ClaudeAgentProvider"/> to run an agentic execution.
/// </summary>
internal sealed class AgentExecutionContextFactory(
    CompactionConfig compactionConfig,
    IModelRegistry? modelRegistry,
    PricingConfig pricingConfig,
    IDialogueTransport dialogueTransport,
    IDialogueTrail dialogueTrail,
    ILogger logger,
    Func<ModelAssignment> resolveModel,
    Func<AnthropicClient> createClient)
{
    public ToolExecutor CreateToolExecutor(
        string repositoryPath,
        FileReadTracker fileReadTracker,
        IProgressReporter progressReporter,
        ISandbox? sandbox = null)
    {
        return new ToolExecutor(
            repositoryPath, logger, fileReadTracker, progressReporter,
            dialogueTransport, dialogueTrail, progressReporter.JobId, sandbox);
    }

    public CostTracker? CreateCostTracker(TokenUsageTracker tracker)
    {
        if (pricingConfig.Models.Count == 0)
            return null;

        var costTracker = new CostTracker(pricingConfig, logger);
        var planningModel = resolveModel();
        costTracker.SetPhaseModel("planning", planningModel.Model);
        return costTracker;
    }

    public ClaudeContextCompactor? CreateCompactor(
        TokenUsageTracker tracker, CostTracker? costTracker)
    {
        if (!compactionConfig.IsEnabled)
            return null;

        var summaryModel = modelRegistry is not null
            ? modelRegistry.GetModel(TaskType.Summarization)
            : new ModelAssignment
            {
                Model = compactionConfig.SummaryModel,
                MaxTokens = AgentDefaults.CompactionMaxTokens
            };

        costTracker?.SetPhaseModel("compaction", summaryModel.Model);

        var compactorClient = createClient();
        return new ClaudeContextCompactor(compactorClient, summaryModel.Model, logger, tracker);
    }

    public RunCostSummary? LogCostSummary(CostTracker? costTracker, TokenUsageTracker tracker)
    {
        tracker.LogSummary(logger);
        if (costTracker is null) return null;

        var costSummary = costTracker.CalculateCost(tracker);
        costTracker.LogCostSummary(costSummary);
        return costSummary;
    }

    public async Task<ScoutResult?> TryRunScoutAsync(
        Plan plan, string repositoryPath,
        TokenUsageTracker tracker, CostTracker? costTracker,
        IProgressReporter? progressReporter,
        CancellationToken cancellationToken)
    {
        if (modelRegistry is null) return null;

        var scoutModel = modelRegistry.GetModel(TaskType.Scout);
        tracker.SetPhase("scout");
        costTracker?.SetPhaseModel("scout", scoutModel.Model);

        logger.LogInformation("Running scout agent with model {Model}", scoutModel.Model);
        ReportDetail(progressReporter, "\ud83d\udd0d Scout: analyzing codebase...", cancellationToken);

        using var scoutClient = createClient();
        var scout = new ScoutAgent(
            scoutClient, scoutModel.Model, scoutModel.MaxTokens, logger, tracker, progressReporter);
        var result = await scout.DiscoverAsync(plan, repositoryPath, cancellationToken);

        ReportDetail(progressReporter,
            $"\ud83d\udd0d Scout: found {result.RelevantFiles.Count} relevant files", cancellationToken);
        return result;
    }

    private void ReportDetail(IProgressReporter? reporter, string text, CancellationToken ct)
    {
        try { reporter?.ReportDetailAsync(text, ct).GetAwaiter().GetResult(); }
        catch (Exception ex) { logger.LogDebug(ex, "Detail reporting failed"); }
    }
}
