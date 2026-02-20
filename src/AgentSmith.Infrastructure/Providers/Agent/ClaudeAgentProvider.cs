using System.Text.Json;
using AgentSmith.Contracts.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.ValueObjects;
using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Providers.Agent;

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

        var systemPrompt = BuildPlanSystemPrompt(codingPrinciples);
        var userPrompt = BuildPlanUserPrompt(ticket, codeAnalysis);

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
        return ParsePlan(rawResponse);
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

        var systemPrompt = BuildExecutionSystemPrompt(codingPrinciples);
        var userMessage = BuildExecutionUserPrompt(plan, repository, scoutResult);

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

        return new ModelAssignment { Model = model, MaxTokens = 8192 };
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

    private static void ReportDetail(IProgressReporter? reporter, string text)
    {
        try { reporter?.ReportDetailAsync(text).GetAwaiter().GetResult(); }
        catch { /* Detail reporting must never abort the pipeline */ }
    }

    private ClaudeContextCompactor? CreateCompactor(
        TokenUsageTracker tracker, CostTracker? costTracker)
    {
        if (!compactionConfig.Enabled)
            return null;

        var summaryModel = modelRegistry is not null
            ? modelRegistry.GetModel(TaskType.Summarization)
            : new ModelAssignment { Model = compactionConfig.SummaryModel, MaxTokens = 2048 };

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

        if (costTracker is null)
            return;

        var costSummary = costTracker.CalculateCost(tracker);
        costTracker.LogCostSummary(costSummary);
    }

    private PromptCacheType ResolveCacheType()
    {
        if (!cacheConfig.Enabled) return PromptCacheType.None;
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

    private static string BuildPlanSystemPrompt(string codingPrinciples)
    {
        return $$"""
            You are a senior software engineer. Analyze the following ticket and codebase,
            then create a detailed implementation plan.

            ## Coding Principles
            {{codingPrinciples}}

            ## Respond in JSON format:
            {
              "summary": "Brief summary of what needs to be done",
              "steps": [
                { "order": 1, "description": "...", "target_file": "...", "change_type": "Create|Modify|Delete" }
              ]
            }

            Respond ONLY with the JSON, no additional text.
            """;
    }

    private static string BuildPlanUserPrompt(Ticket ticket, CodeAnalysis codeAnalysis)
    {
        var files = string.Join('\n', codeAnalysis.FileStructure.Take(200));
        var deps = string.Join('\n', codeAnalysis.Dependencies);

        return $"""
            ## Ticket
            **ID:** {ticket.Id}
            **Title:** {ticket.Title}
            **Description:** {ticket.Description}
            **Acceptance Criteria:** {ticket.AcceptanceCriteria ?? "None specified"}

            ## Codebase Analysis
            **Language:** {codeAnalysis.Language ?? "Unknown"}
            **Framework:** {codeAnalysis.Framework ?? "Unknown"}

            ### Dependencies
            {deps}

            ### File Structure
            {files}
            """;
    }

    private static string BuildExecutionSystemPrompt(string codingPrinciples)
    {
        // Coding principles placed first for optimal prompt caching:
        // they form the longest stable prefix and will be cached after the first call.
        return $"""
            ## Coding Principles
            {codingPrinciples}

            ## Role
            You are a senior software engineer implementing code changes.
            You have access to tools to read, write, and list files in the repository,
            as well as run shell commands.

            ## Instructions
            - Read existing files before modifying them to understand the current state.
            - Write complete file contents when using write_file (not diffs).
            - Follow the coding principles strictly.
            - Run build and test commands to verify your changes (e.g. dotnet build, dotnet test, npm run build, npm test).
            - NEVER run long-running server processes (dotnet run, npm start, python -m http.server, etc.) — they will time out and block the pipeline.
            - NEVER run interactive commands that require user input.
            - Before each tool call, briefly state what you are doing and why (e.g. "Reading Program.cs to understand the current endpoint structure").
            - When done, stop calling tools and summarize what you did.
            """;
    }

    private static string BuildExecutionUserPrompt(
        Plan plan, Repository repository, ScoutResult? scoutResult = null)
    {
        var steps = string.Join('\n', plan.Steps.Select(
            s => $"  {s.Order}. [{s.ChangeType}] {s.Description} → {s.TargetFile}"));

        var scoutSection = "";
        if (scoutResult is not null && scoutResult.RelevantFiles.Count > 0)
        {
            var files = string.Join('\n', scoutResult.RelevantFiles.Select(f => $"  - {f}"));
            scoutSection = $"""

                ## Scout Results
                The following files have been identified as relevant by the scout agent:
                {files}

                **Scout Summary:** {scoutResult.ContextSummary}

                """;
        }

        var startInstruction = scoutResult is not null
            ? "The scout has already explored the codebase. Proceed directly with implementation."
            : "Start by listing the relevant files, then implement each step.";

        return $"""
            Execute the following implementation plan in repository at: {repository.LocalPath}
            Branch: {repository.CurrentBranch}
            {scoutSection}
            ## Plan
            **Summary:** {plan.Summary}

            **Steps:**
            {steps}

            {startInstruction}
            """;
    }

    private static string ExtractTextResponse(MessageResponse response)
    {
        var textContent = response.Content.OfType<TextContent>().FirstOrDefault();
        return textContent?.Text
            ?? throw new ProviderException("Claude", "Claude returned no text response.");
    }

    private static Plan ParsePlan(string rawJson)
    {
        try
        {
            var cleaned = StripMarkdownCodeBlock(rawJson);
            using var doc = JsonDocument.Parse(cleaned);
            var root = doc.RootElement;

            var summary = root.GetProperty("summary").GetString() ?? "";
            var steps = root.GetProperty("steps").EnumerateArray()
                .Select(ParsePlanStep)
                .ToList();

            return new Plan(summary, steps, rawJson);
        }
        catch (JsonException ex)
        {
            throw new ProviderException(
                "Claude", $"Failed to parse plan response from Claude: {ex.Message}", ex);
        }
    }

    private static string StripMarkdownCodeBlock(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.StartsWith("```"))
        {
            var firstNewline = trimmed.IndexOf('\n');
            if (firstNewline >= 0)
                trimmed = trimmed[(firstNewline + 1)..];
        }
        if (trimmed.EndsWith("```"))
        {
            trimmed = trimmed[..^3].TrimEnd();
        }
        return trimmed;
    }

    private static PlanStep ParsePlanStep(JsonElement element)
    {
        var order = element.GetProperty("order").GetInt32();
        var description = element.GetProperty("description").GetString() ?? "";
        var targetFile = element.TryGetProperty("target_file", out var tf)
            ? new FilePath(tf.GetString()!)
            : null;
        var changeType = element.TryGetProperty("change_type", out var ct)
            ? ct.GetString() ?? "Modify"
            : "Modify";

        return new PlanStep(order, description, targetFile, changeType);
    }
}
