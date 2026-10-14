using AgentSmith.Contracts.Turns;

namespace AgentSmith.Application.Services.Turns;

/// <summary>
/// Passes every step on to the enclosing observer under the name of the sub-agent that took it,
/// so a fan-out's lines say which child read what.
/// </summary>
public sealed class NamedTurnActivityObserver(ITurnActivityObserver inner, string subAgentName)
    : ITurnActivityObserver
{
    public Task ReportAsync(TurnActivity activity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(activity);
        var name = activity.Name is null ? subAgentName : $"{subAgentName} › {activity.Name}";
        return inner.ReportAsync(activity with { Name = name }, cancellationToken);
    }
}
