using System.Text.Json;
using AgentSmith.Contracts.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.ValueObjects;
using GenerativeAI;
using GenerativeAI.Types;
using Microsoft.Extensions.Logging;
using TaskType = AgentSmith.Contracts.Providers.TaskType;

namespace AgentSmith.Infrastructure.Providers.Agent;

/// <summary>
/// AI agent provider using the Google Gemini API.
/// Supports Gemini 2.5 Flash, Gemini 2.5 Pro, and other Gemini models with function calling.
/// </summary>
public sealed class GeminiAgentProvider(
    string apiKey,
    string model,
    IModelRegistry? modelRegistry,
    PricingConfig pricingConfig,
    ILogger<GeminiAgentProvider> logger) : IAgentProvider
{
    public string ProviderType => "Gemini";

    public async Task<Plan> GeneratePlanAsync(
        Ticket ticket,
        CodeAnalysis codeAnalysis,
        string codingPrinciples,
        CancellationToken cancellationToken = default)
    {
        var planModel = ResolveModel(TaskType.Planning);
        var genModel = CreateModel(planModel.Model);

        var systemPrompt = BuildPlanSystemPrompt(codingPrinciples);
        var userPrompt = BuildPlanUserPrompt(ticket, codeAnalysis);

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

        return ParsePlan(rawResponse);
    }

    public async Task<IReadOnlyList<CodeChange>> ExecutePlanAsync(
        Plan plan,
        Repository repository,
        string codingPrinciples,
        CancellationToken cancellationToken = default)
    {
        var tracker = new TokenUsageTracker();
        var costTracker = CreateCostTracker(tracker);

        tracker.SetPhase("primary");
        var primaryModel = ResolveModel(TaskType.Primary);
        costTracker?.SetPhaseModel("primary", primaryModel.Model);

        var fileReadTracker = new FileReadTracker();
        var toolExecutor = new ToolExecutor(repository.LocalPath, logger, fileReadTracker);
        var genModel = CreateModel(primaryModel.Model);

        var loop = new GeminiAgenticLoop(
            genModel, toolExecutor, logger, tracker);

        var systemPrompt = BuildExecutionSystemPrompt(codingPrinciples);
        var userMessage = BuildExecutionUserPrompt(plan, repository);

        var changes = await loop.RunAsync(systemPrompt, userMessage, cancellationToken);

        logger.LogInformation(
            "Gemini agentic execution completed with {Count} file changes", changes.Count);

        LogCostSummary(costTracker, tracker);

        return changes;
    }

    private ModelAssignment ResolveModel(TaskType taskType)
    {
        if (modelRegistry is not null)
            return modelRegistry.GetModel(taskType);

        return new ModelAssignment { Model = model, MaxTokens = 8192 };
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

    private void LogCostSummary(CostTracker? costTracker, TokenUsageTracker tracker)
    {
        tracker.LogSummary(logger);
        if (costTracker is null) return;

        var costSummary = costTracker.CalculateCost(tracker);
        costTracker.LogCostSummary(costSummary);
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
            - Run build/test commands to verify your changes compile.
            - When done, stop calling tools and summarize what you did.
            """;
    }

    private static string BuildExecutionUserPrompt(Plan plan, Repository repository)
    {
        var steps = string.Join('\n', plan.Steps.Select(
            s => $"  {s.Order}. [{s.ChangeType}] {s.Description} → {s.TargetFile}"));

        return $"""
            Execute the following implementation plan in repository at: {repository.LocalPath}
            Branch: {repository.CurrentBranch}

            ## Plan
            **Summary:** {plan.Summary}

            **Steps:**
            {steps}

            Start by listing the relevant files, then implement each step.
            """;
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
                "Gemini", $"Failed to parse plan response from Gemini: {ex.Message}", ex);
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
            trimmed = trimmed[..^3].TrimEnd();
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
