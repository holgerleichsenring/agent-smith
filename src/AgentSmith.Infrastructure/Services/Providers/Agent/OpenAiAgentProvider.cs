using System.Diagnostics;
using AgentSmith.Contracts.Models;
using AgentSmith.Infrastructure.Models;
using System.ClientModel;
using System.ClientModel.Primitives;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Providers.Agent.Cost;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;

namespace AgentSmith.Infrastructure.Services.Providers.Agent;

/// <summary>
/// AI agent provider using the OpenAI Chat Completions API.
/// Supports GPT-4.1, GPT-4.1-mini, and other OpenAI models with tool calling.
/// </summary>
public class OpenAiAgentProvider(
    string apiKey,
    string model,
    RetryConfig retryConfig,
    IModelRegistry? modelRegistry,
    PricingConfig pricingConfig,
    ILogger<OpenAiAgentProvider> logger,
    AgentPromptBuilder promptBuilder,
    Uri? endpoint = null) : IAgentProvider
{
    public virtual string ProviderType => "OpenAI";

    public Task<Plan> GeneratePlanAsync(
        Ticket ticket,
        ProjectMap projectMap,
        string codingPrinciples,
        string? codeMap,
        string? projectContext,
        IReadOnlyList<TicketImageAttachment>? images,
        CancellationToken cancellationToken)
    {
        return GeneratePlanCoreAsync(ticket, projectMap, codingPrinciples, codeMap, projectContext, images, cancellationToken);
    }

    private async Task<Plan> GeneratePlanCoreAsync(
        Ticket ticket,
        ProjectMap projectMap,
        string codingPrinciples,
        string? codeMap,
        string? projectContext,
        IReadOnlyList<TicketImageAttachment>? images,
        CancellationToken cancellationToken)
    {
        var planModel = ResolveModel(TaskType.Planning);
        var client = CreateChatClient(planModel);

        var systemPrompt = promptBuilder.BuildPlanSystemPrompt(codingPrinciples, codeMap, projectContext);
        var userPrompt = promptBuilder.BuildPlanUserPrompt(ticket, projectMap);

        var contentParts = new List<ChatMessageContentPart>();

        // p87: Add ticket images as vision content parts
        if (images is { Count: > 0 })
        {
            foreach (var img in images)
            {
                contentParts.Add(ChatMessageContentPart.CreateImagePart(
                    BinaryData.FromBytes(img.Content), img.MediaType));
            }

            logger.LogInformation("Including {Count} image(s) in plan generation prompt", images.Count);
        }

        contentParts.Add(ChatMessageContentPart.CreateTextPart(userPrompt));

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(contentParts)
        };

        var options = new ChatCompletionOptions { MaxOutputTokenCount = planModel.MaxTokens };
        ChatCompletion completion = await client.CompleteChatAsync(
            messages, options, cancellationToken);

        var rawResponse = completion.Content[0].Text;
        return PlanParser.Parse("OpenAI", rawResponse);
    }

    public async Task<AgentExecutionResult> ExecutePlanAsync(
        Plan plan,
        Repository repository,
        string codingPrinciples,
        string? codeMap,
        string? projectContext,
        IProgressReporter progressReporter,
        ISandbox? sandbox,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var tracker = new TokenUsageTracker();
        var costTracker = CreateCostTracker(tracker);

        tracker.SetPhase("primary");
        var primaryModel = ResolveModel(TaskType.Primary);
        costTracker.SetPhaseModel("primary", primaryModel.Model);

        var fileReadTracker = new FileReadTracker();
        var toolExecutor = new ToolExecutor(
            repository.LocalPath, logger, fileReadTracker, progressReporter,
            sandbox: sandbox);
        var client = CreateChatClient(primaryModel);

        var loop = new OpenAiAgenticLoop(
            client, toolExecutor, logger, tracker, costTracker, progressReporter, 25);

        var systemPrompt = promptBuilder.BuildExecutionSystemPrompt(codingPrinciples, codeMap, projectContext);
        var userMessage = promptBuilder.BuildExecutionUserPrompt(plan, repository);

        var changes = await loop.RunAsync(systemPrompt, userMessage, cancellationToken);
        var decisions = toolExecutor.GetDecisions();
        sw.Stop();

        logger.LogInformation(
            "OpenAI agentic execution completed with {Count} file changes and {Decisions} decisions in {Seconds}s",
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

    protected virtual ChatClient CreateChatClient(ModelAssignment assignment)
    {
        var factory = new ResilientHttpClientFactory(retryConfig, logger);
        var httpClient = factory.Create();
        var clientOptions = new OpenAIClientOptions { Transport = new HttpClientPipelineTransport(httpClient) };
        if (endpoint is not null)
            clientOptions.Endpoint = endpoint;
        var openAiClient = new OpenAIClient(new ApiKeyCredential(apiKey), clientOptions);
        return openAiClient.GetChatClient(assignment.Model);
    }

    protected virtual OpenAiCostTracker CreateCostTracker(TokenUsageTracker tracker)
    {
        var costTracker = new OpenAiCostTracker(pricingConfig, logger, tracker);
        var planningModel = ResolveModel(TaskType.Planning);
        costTracker.SetPhaseModel("planning", planningModel.Model);
        return costTracker;
    }

    private RunCostSummary? LogCostSummary(OpenAiCostTracker costTracker, TokenUsageTracker tracker)
    {
        costTracker.LogTokenSummary(logger);
        if (pricingConfig.Models.Count == 0) return null;

        costTracker.LogCostSummary(logger);
        return costTracker.CalculateCost();
    }
}
