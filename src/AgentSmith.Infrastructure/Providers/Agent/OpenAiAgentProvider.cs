using System.ClientModel;
using System.ClientModel.Primitives;
using System.Text.Json;
using System.Text.Json.Nodes;
using AgentSmith.Contracts.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;

namespace AgentSmith.Infrastructure.Providers.Agent;

/// <summary>
/// AI agent provider using the OpenAI Chat Completions API.
/// Supports GPT-4.1, GPT-4.1-mini, and other OpenAI models with tool calling.
/// </summary>
public sealed class OpenAiAgentProvider(
    string apiKey,
    string model,
    RetryConfig retryConfig,
    IModelRegistry? modelRegistry,
    PricingConfig pricingConfig,
    ILogger<OpenAiAgentProvider> logger) : IAgentProvider
{
    public string ProviderType => "OpenAI";

    public async Task<Plan> GeneratePlanAsync(
        Ticket ticket,
        CodeAnalysis codeAnalysis,
        string codingPrinciples,
        CancellationToken cancellationToken = default)
    {
        var planModel = ResolveModel(TaskType.Planning);
        var client = CreateChatClient(planModel.Model);

        var systemPrompt = BuildPlanSystemPrompt(codingPrinciples);
        var userPrompt = BuildPlanUserPrompt(ticket, codeAnalysis);

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(userPrompt)
        };

        var options = new ChatCompletionOptions { MaxOutputTokenCount = planModel.MaxTokens };
        ChatCompletion completion = await client.CompleteChatAsync(
            messages, options, cancellationToken);

        var rawResponse = completion.Content[0].Text;
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
        var client = CreateChatClient(primaryModel.Model);

        var loop = new OpenAiAgenticLoop(
            client, toolExecutor, logger, tracker);

        var systemPrompt = BuildExecutionSystemPrompt(codingPrinciples);
        var userMessage = BuildExecutionUserPrompt(plan, repository);

        var changes = await loop.RunAsync(systemPrompt, userMessage, cancellationToken);

        logger.LogInformation(
            "OpenAI agentic execution completed with {Count} file changes", changes.Count);

        LogCostSummary(costTracker, tracker);

        return changes;
    }

    private ModelAssignment ResolveModel(TaskType taskType)
    {
        if (modelRegistry is not null)
            return modelRegistry.GetModel(taskType);

        return new ModelAssignment { Model = model, MaxTokens = 8192 };
    }

    private ChatClient CreateChatClient(string modelId)
    {
        var factory = new ResilientHttpClientFactory(retryConfig, logger);
        var httpClient = factory.Create();
        var clientOptions = new OpenAIClientOptions { Transport = new HttpClientPipelineTransport(httpClient) };
        var openAiClient = new OpenAIClient(new ApiKeyCredential(apiKey), clientOptions);
        return openAiClient.GetChatClient(modelId);
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
                "OpenAI", $"Failed to parse plan response from OpenAI: {ex.Message}", ex);
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
