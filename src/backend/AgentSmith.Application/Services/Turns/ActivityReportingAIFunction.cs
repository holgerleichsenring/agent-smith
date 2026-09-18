using System.Text.Json;
using AgentSmith.Application.Services.Events;
using AgentSmith.Contracts.Turns;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Turns;

/// <summary>
/// 2026-09-17-042ee: wraps an <see cref="AIFunction"/> so the turn's owner is told the call
/// was made, before it is made — a file read that takes twenty seconds is announced when it
/// starts, not when it ends.
/// <para>
/// This is NOT <see cref="EventPublishingAIFunction"/>. That one publishes run events, which
/// this turn's tools are never wrapped for (nothing constructs it outside the skill-call
/// runtime, which has no production caller), and which would go to the run's trail rather
/// than to the person holding the conversation. What leaves here is the tool's name and the
/// whitelisted argument summary, to one dialog group.
/// </para>
/// </summary>
public sealed class ActivityReportingAIFunction(
    AIFunction inner, ITurnActivityObserverAccessor activity) : AIFunction
{
    public override string Name => inner.Name;
    public override string Description => inner.Description;
    public override JsonElement JsonSchema => inner.JsonSchema;
    public override JsonSerializerOptions JsonSerializerOptions => inner.JsonSerializerOptions;

    protected override async ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        await activity.ReportAsync(
            new TurnActivity(
                TurnActivityKind.Tool, inner.Name, ToolArgumentFacts.SummarizeForOperator(arguments)),
            cancellationToken);
        return await inner.InvokeAsync(arguments, cancellationToken);
    }
}
