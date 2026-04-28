using System.Diagnostics;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Infrastructure.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.Models;
using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Agent;

/// <summary>AI agent provider using the Anthropic Claude API.</summary>
public sealed class ClaudeAgentProvider(
    string apiKey,
    string model,
    RetryConfig retryConfig,
    CacheConfig cacheConfig,
    CompactionConfig compactionConfig,
    IModelRegistry? modelRegistry,
    PricingConfig pricingConfig,
    ILogger<ClaudeAgentProvider> logger,
    IDialogueTransport dialogueTransport,
    IDialogueTrail dialogueTrail,
    AgentPromptBuilder promptBuilder) : IAgentProvider
{
    public string ProviderType => "Claude";

    public Task<Plan> GeneratePlanAsync(
        Ticket ticket,
        CodeAnalysis codeAnalysis,
        string codingPrinciples,
        string? codeMap,
        string? projectContext,
        IReadOnlyList<TicketImageAttachment>? images,
        CancellationToken cancellationToken)
    {
        return GeneratePlanCoreAsync(ticket, codeAnalysis, codingPrinciples, codeMap, projectContext, images, cancellationToken);
    }

    private async Task<Plan> GeneratePlanCoreAsync(
        Ticket ticket,
        CodeAnalysis codeAnalysis,
        string codingPrinciples,
        string? codeMap,
        string? projectContext,
        IReadOnlyList<TicketImageAttachment>? images,
        CancellationToken cancellationToken)
    {
        using var client = CreateResilientClient();
        var systemPrompt = promptBuilder.BuildPlanSystemPrompt(codingPrinciples, codeMap, projectContext);
        var userPrompt = promptBuilder.BuildPlanUserPrompt(ticket, codeAnalysis);
        var planModel = ResolveModel(TaskType.Planning);

        var userContent = new List<ContentBase>();

        // p87: Add ticket images as vision content blocks
        if (images is { Count: > 0 })
        {
            foreach (var img in images)
            {
                userContent.Add(new ImageContent
                {
                    Source = new ImageSource
                    {
                        MediaType = img.MediaType,
                        Data = img.Base64
                    }
                });
            }

            logger.LogInformation("Including {Count} image(s) in plan generation prompt", images.Count);
        }

        userContent.Add(new TextContent { Text = userPrompt });

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
                        Content = userContent
                    }
                },
                Stream = false,
                PromptCaching = CacheTypeResolver.Resolve(cacheConfig)
            },
            cancellationToken);

        return PlanParser.Parse("Claude", ExtractTextResponse(response));
    }

    public async Task<AgentExecutionResult> ExecutePlanAsync(
        Plan plan, Repository repository,
        string codingPrinciples, string? codeMap, string? projectContext,
        IProgressReporter progressReporter, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var ctxFactory = CreateContextFactory();
        var tracker = new TokenUsageTracker();
        var costTracker = ctxFactory.CreateCostTracker(tracker);

        var scoutResult = await ctxFactory.TryRunScoutAsync(
            plan, repository.LocalPath, tracker, costTracker, progressReporter, cancellationToken);

        tracker.SetPhase("primary");
        var primaryModel = ResolveModel(TaskType.Primary);
        costTracker?.SetPhaseModel("primary", primaryModel.Model);

        var toolExecutor = ctxFactory.CreateToolExecutor(
            repository.LocalPath, new FileReadTracker(), progressReporter);
        using var client = CreateResilientClient();
        var loop = new AgenticLoop(client, primaryModel.Model, toolExecutor, logger,
            cacheConfig, tracker, compactionConfig,
            ctxFactory.CreateCompactor(tracker, costTracker), progressReporter, 25);

        var systemPrompt = promptBuilder.BuildExecutionSystemPrompt(codingPrinciples, codeMap, projectContext);
        var userMessage = promptBuilder.BuildExecutionUserPrompt(plan, repository, scoutResult);
        var changes = await loop.RunAsync(systemPrompt, userMessage, cancellationToken);
        sw.Stop();
        logger.LogInformation(
            "Agentic execution completed in {Seconds}s with {Count} changes, {Decisions} decisions",
            (int)sw.Elapsed.TotalSeconds, changes.Count, toolExecutor.GetDecisions().Count);
        var costSummary = ctxFactory.LogCostSummary(costTracker, tracker);
        return new AgentExecutionResult(
            changes, costSummary, (int)sw.Elapsed.TotalSeconds, toolExecutor.GetDecisions());
    }

    private AgentExecutionContextFactory CreateContextFactory() =>
        new(compactionConfig, modelRegistry, pricingConfig,
            dialogueTransport, dialogueTrail, logger,
            () => ResolveModel(TaskType.Planning), CreateResilientClient);

    private ModelAssignment ResolveModel(TaskType taskType) =>
        modelRegistry?.GetModel(taskType)
        ?? new ModelAssignment { Model = model, MaxTokens = AgentDefaults.DefaultMaxTokens };

    private AnthropicClient CreateResilientClient() =>
        new(apiKey, new ResilientHttpClientFactory(retryConfig, logger).Create());

    private static string ExtractTextResponse(MessageResponse response) =>
        response.Content.OfType<TextContent>().FirstOrDefault()?.Text
        ?? throw new ProviderException("Claude", "Claude returned no text response.");
}
