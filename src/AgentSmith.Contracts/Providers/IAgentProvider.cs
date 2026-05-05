using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Contracts.Providers;

/// <summary>
/// Provides AI agent capabilities (plan generation, agentic code execution).
/// </summary>
public interface IAgentProvider : ITypedProvider
{
    Task<Plan> GeneratePlanAsync(
        Ticket ticket,
        ProjectMap projectMap,
        string codingPrinciples,
        string? codeMap,
        string? projectContext,
        CancellationToken cancellationToken)
        => GeneratePlanAsync(ticket, projectMap, codingPrinciples, codeMap, projectContext, null, cancellationToken);

    Task<Plan> GeneratePlanAsync(
        Ticket ticket,
        ProjectMap projectMap,
        string codingPrinciples,
        string? codeMap,
        string? projectContext,
        IReadOnlyList<TicketImageAttachment>? images,
        CancellationToken cancellationToken);

    Task<AgentExecutionResult> ExecutePlanAsync(
        Plan plan,
        Repository repository,
        string codingPrinciples,
        string? codeMap,
        string? projectContext,
        IProgressReporter progressReporter,
        CancellationToken cancellationToken)
        => ExecutePlanAsync(plan, repository, codingPrinciples, codeMap, projectContext,
            progressReporter, sandbox: null, cancellationToken);

    Task<AgentExecutionResult> ExecutePlanAsync(
        Plan plan,
        Repository repository,
        string codingPrinciples,
        string? codeMap,
        string? projectContext,
        IProgressReporter progressReporter,
        ISandbox? sandbox,
        CancellationToken cancellationToken);
}
