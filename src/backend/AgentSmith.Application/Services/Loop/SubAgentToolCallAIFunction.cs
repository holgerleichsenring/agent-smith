using System.Text.Json;
using AgentSmith.Application.Services.Events;
using AgentSmith.Contracts.Events;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Loop;

/// <summary>
/// 2026-11-19-687eb: wraps one function a child was granted, so each call it makes is a
/// <see cref="SubAgentToolCallEvent"/> on the run, attributed to that child before the call runs. The run page lists a
/// sub-agent's calls from these, for a coding run's children and a design turn's alike. Metadata only: the tool's name
/// and the whitelisted argument summary.
/// </summary>
public sealed class SubAgentToolCallAIFunction(
    AIFunction inner, IEventPublisher publisher, string runId, string subAgentId) : AIFunction
{
    public override string Name => inner.Name;
    public override string Description => inner.Description;
    public override JsonElement JsonSchema => inner.JsonSchema;
    public override JsonSerializerOptions JsonSerializerOptions => inner.JsonSerializerOptions;

    protected override async ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        await publisher.PublishAsync(
            new SubAgentToolCallEvent(
                runId, subAgentId, inner.Name, ToolArgumentFacts.Summarize(arguments), DateTimeOffset.UtcNow),
            cancellationToken);
        return await inner.InvokeAsync(arguments, cancellationToken);
    }
}
