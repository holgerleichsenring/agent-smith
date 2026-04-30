using System.Diagnostics;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Agent;

/// <summary>
/// Agent provider for locally-hosted LLMs via Ollama (OpenAI-compatible API).
/// Supports native tool calling or structured text fallback.
/// </summary>
public sealed class OllamaAgentProvider(
    string model,
    OpenAiCompatibleClient client,
    bool hasToolCalling,
    IModelRegistry? modelRegistry,
    ILogger<OllamaAgentProvider> logger,
    AgentPromptBuilder promptBuilder) : IAgentProvider
{
    public string ProviderType => "ollama";

    public Task<Plan> GeneratePlanAsync(
        Ticket ticket, ProjectMap projectMap, string codingPrinciples,
        string? codeMap, string? projectContext,
        IReadOnlyList<TicketImageAttachment>? images,
        CancellationToken cancellationToken)
    {
        if (images is { Count: > 0 })
            logger.LogWarning("Ollama vision for ticket images not yet implemented, {Count} image(s) ignored", images.Count);
        return GeneratePlanCoreAsync(ticket, projectMap, codingPrinciples, codeMap, projectContext, cancellationToken);
    }

    private async Task<Plan> GeneratePlanCoreAsync(
        Ticket ticket, ProjectMap projectMap, string codingPrinciples,
        string? codeMap, string? projectContext, CancellationToken cancellationToken)
    {
        var planModel = ResolveModel(TaskType.Planning);
        logger.LogInformation("Generating plan via Ollama ({Model})", planModel.Model);

        var systemPrompt = promptBuilder.BuildPlanSystemPrompt(
            codingPrinciples, codeMap, projectContext);
        var userPrompt = promptBuilder.BuildPlanUserPrompt(ticket, projectMap);

        var messages = new System.Text.Json.Nodes.JsonArray
        {
            Msg("system", systemPrompt),
            Msg("user", userPrompt)
        };

        var response = await client.ChatCompleteAsync(
            planModel.Model, messages, null, planModel.MaxTokens, cancellationToken);

        var content = response.GetProperty("choices")[0]
            .GetProperty("message").GetProperty("content").GetString() ?? "";

        return PlanParser.Parse("Ollama", content);
    }

    public async Task<AgentExecutionResult> ExecutePlanAsync(
        Plan plan, Repository repository, string codingPrinciples,
        string? codeMap, string? projectContext,
        IProgressReporter progressReporter, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var primaryModel = ResolveModel(TaskType.Primary);

        logger.LogInformation(
            "Executing plan via Ollama ({Model}, tool_calling={HasTools})",
            primaryModel.Model, hasToolCalling);

        var fileReadTracker = new FileReadTracker();
        var toolExecutor = new ToolExecutor(
            repository.LocalPath, logger, fileReadTracker, progressReporter);

        var systemPrompt = promptBuilder.BuildExecutionSystemPrompt(
            codingPrinciples, codeMap, projectContext);
        var userMessage = promptBuilder.BuildExecutionUserPrompt(plan, repository);

        IReadOnlyList<CodeChange> changes;

        if (hasToolCalling)
        {
            var loop = new OllamaAgenticLoop(
                client, primaryModel.Model, toolExecutor, logger, progressReporter, 25);
            changes = await loop.RunAsync(systemPrompt, userMessage, cancellationToken);
        }
        else
        {
            logger.LogWarning("Ollama model {Model} does not support tool calling. " +
                              "Structured text fallback not yet implemented.", primaryModel.Model);
            changes = toolExecutor.GetChanges();
        }

        var decisions = toolExecutor.GetDecisions();
        sw.Stop();

        logger.LogInformation(
            "Ollama execution completed: {Changes} files, {Decisions} decisions in {Seconds}s",
            changes.Count, decisions.Count, (int)sw.Elapsed.TotalSeconds);

        return new AgentExecutionResult(changes, null, (int)sw.Elapsed.TotalSeconds, decisions);
    }

    private ModelAssignment ResolveModel(TaskType taskType)
    {
        if (modelRegistry is not null)
            return modelRegistry.GetModel(taskType);

        return new ModelAssignment { Model = model, MaxTokens = AgentDefaults.DefaultMaxTokens };
    }

    private static System.Text.Json.Nodes.JsonObject Msg(string role, string content) =>
        new() { ["role"] = role, ["content"] = content };
}
