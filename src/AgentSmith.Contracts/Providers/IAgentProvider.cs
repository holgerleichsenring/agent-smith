using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Contracts.Providers;

/// <summary>
/// Provides AI agent capabilities (plan generation, agentic code execution).
/// </summary>
public interface IAgentProvider : ITypedProvider
{
    Task<Plan> GeneratePlanAsync(
        Ticket ticket,
        CodeAnalysis codeAnalysis,
        string codingPrinciples,
        string? codeMap,
        string? projectContext,
        CancellationToken cancellationToken);

    Task<AgentExecutionResult> ExecutePlanAsync(
        Plan plan,
        Repository repository,
        string codingPrinciples,
        string? codeMap,
        string? projectContext,
        IProgressReporter progressReporter,
        CancellationToken cancellationToken);
}
