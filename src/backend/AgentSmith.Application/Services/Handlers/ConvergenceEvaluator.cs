using AgentSmith.Application.Models;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Default <see cref="IConvergenceEvaluator"/>: composes the reasoning chat
/// client with the convergence prompt, posts the observation summary, and
/// hands the response to <see cref="ConvergenceResultParser"/>. On parse
/// failure, returns a non-consensus verdict so the caller can decide whether
/// to keep iterating.
/// </summary>
public sealed class ConvergenceEvaluator(
    IChatClientFactory chatClientFactory,
    IPromptCatalog prompts,
    ConvergenceResultParser resultParser,
    IRunContextAccessor runContext,
    ILogger<ConvergenceEvaluator> logger) : IConvergenceEvaluator
{
    public async Task<ConvergenceResult> EvaluateAsync(
        AgentConfig agent,
        IReadOnlyList<SkillObservation> observations,
        Action<ChatResponse> costSink,
        CancellationToken cancellationToken)
    {
        var chat = chatClientFactory.Create(agent, TaskType.Reasoning);
        var maxTokens = chatClientFactory.GetMaxOutputTokens(agent, TaskType.Reasoning);
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, prompts.Get("convergence-system")),
            new(ChatRole.User, BuildUserPrompt(observations)),
        };
        using var _scope = runContext.BeginCallScope(
            "convergence-evaluator", SkillExecutionPhase.Synthesize.ToString());
        var response = await chat.GetResponseAsync(
            messages, new ChatOptions { MaxOutputTokens = maxTokens }, cancellationToken);
        costSink(response);
        var result = resultParser.Parse(response.Text ?? string.Empty, observations, logger);
        if (result is not null) return result;
        logger.LogWarning("Failed to parse convergence result, treating as no consensus");
        return new ConvergenceResult(
            Consensus: false, Observations: observations, Links: [], AdditionalRoles: [],
            Blocking: observations.Where(o => o.Blocking).ToList(),
            NonBlocking: observations.Where(o => !o.Blocking).ToList());
    }

    private static string BuildUserPrompt(IReadOnlyList<SkillObservation> observations)
    {
        var activeRoles = observations.Select(o => o.Role).Distinct().ToList();
        var summary = string.Join("\n", observations.Select(o =>
            $"[{o.Id}] {o.Role} | {o.Concern} | {o.Severity} | blocking={o.Blocking} | confidence={o.Confidence}\n"
            + $"  {o.Description}\n"
            + (string.IsNullOrWhiteSpace(o.Suggestion) ? "" : $"  → {o.Suggestion}\n")));
        return $"""
            ## Active Roles
            {string.Join(", ", activeRoles)}

            ## All Observations
            {summary}

            Analyze these observations for consensus. Respond with JSON only.
            """;
    }
}
