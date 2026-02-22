using AgentSmith.Infrastructure.Models;
using System.ClientModel;
using System.ClientModel.Primitives;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;

namespace AgentSmith.Infrastructure.Services.Providers.Agent;

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

        var systemPrompt = AgentPromptBuilder.BuildPlanSystemPrompt(codingPrinciples);
        var userPrompt = AgentPromptBuilder.BuildPlanUserPrompt(ticket, codeAnalysis);

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(userPrompt)
        };

        var options = new ChatCompletionOptions { MaxOutputTokenCount = planModel.MaxTokens };
        ChatCompletion completion = await client.CompleteChatAsync(
            messages, options, cancellationToken);

        var rawResponse = completion.Content[0].Text;
        return PlanParser.Parse("OpenAI", rawResponse);
    }

    public async Task<IReadOnlyList<CodeChange>> ExecutePlanAsync(
        Plan plan,
        Repository repository,
        string codingPrinciples,
        IProgressReporter? progressReporter = null,
        CancellationToken cancellationToken = default)
    {
        var tracker = new TokenUsageTracker();
        var costTracker = CreateCostTracker(tracker);

        tracker.SetPhase("primary");
        var primaryModel = ResolveModel(TaskType.Primary);
        costTracker?.SetPhaseModel("primary", primaryModel.Model);

        var fileReadTracker = new FileReadTracker();
        var toolExecutor = new ToolExecutor(
            repository.LocalPath, logger, fileReadTracker, progressReporter);
        var client = CreateChatClient(primaryModel.Model);

        var loop = new OpenAiAgenticLoop(
            client, toolExecutor, logger, tracker, progressReporter);

        var systemPrompt = AgentPromptBuilder.BuildExecutionSystemPrompt(codingPrinciples);
        var userMessage = AgentPromptBuilder.BuildExecutionUserPrompt(plan, repository);

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

        return new ModelAssignment { Model = model, MaxTokens = AgentDefaults.DefaultMaxTokens };
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
}
