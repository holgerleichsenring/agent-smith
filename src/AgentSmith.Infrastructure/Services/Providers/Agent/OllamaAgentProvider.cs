using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Agent;

/// <summary>
/// Agent provider for locally-hosted LLMs via Ollama (OpenAI-compatible API).
/// Supports native tool calling or structured text fallback depending on model capabilities.
/// </summary>
public sealed class OllamaAgentProvider(
    string model,
    string endpoint,
    bool hasToolCalling,
    IModelRegistry? modelRegistry,
    PricingConfig? pricing,
    ILogger<OllamaAgentProvider> logger) : IAgentProvider
{
    public string ProviderType => "ollama";

    public async Task<Plan> GeneratePlanAsync(
        Ticket ticket, CodeAnalysis codeAnalysis, string codingPrinciples,
        string? codeMap, string? projectContext, CancellationToken cancellationToken)
    {
        logger.LogInformation("Generating plan via Ollama ({Model}) at {Endpoint}", model, endpoint);

        // Ollama uses same plan generation as OpenAI (JSON response, no tool calling)
        // Delegate to shared OpenAI-compatible logic
        var client = new OpenAiCompatibleClient(endpoint + "/v1", null, logger);

        // For now, return a placeholder — full implementation mirrors OpenAiAgentProvider
        // with the OpenAiCompatibleClient replacing the official OpenAI SDK
        throw new NotImplementedException(
            "OllamaAgentProvider.GeneratePlanAsync requires OpenAiCompatibleClient integration. " +
            "This is a structural placeholder — wire the client in a follow-up.");
    }

    public async Task<AgentExecutionResult> ExecutePlanAsync(
        Plan plan, Repository repository, string codingPrinciples,
        string? codeMap, string? projectContext,
        IProgressReporter progressReporter, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Executing plan via Ollama ({Model}, tool_calling={HasTools}) at {Endpoint}",
            model, hasToolCalling, endpoint);

        if (hasToolCalling)
        {
            // Native tool calling — same flow as OpenAI
            logger.LogDebug("Using native tool calling");
        }
        else
        {
            // Structured text fallback — prompt returns JSON action list
            logger.LogDebug("Using structured text fallback (no tool calling support)");
        }

        // Structural placeholder — full implementation in follow-up
        throw new NotImplementedException(
            "OllamaAgentProvider.ExecutePlanAsync requires tool executor integration. " +
            "This is a structural placeholder — wire the client in a follow-up.");
    }
}
