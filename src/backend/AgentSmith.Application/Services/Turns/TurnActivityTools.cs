using AgentSmith.Contracts.Turns;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Turns;

/// <summary>
/// 2026-09-17-042ee: builds the tool surface a turn reports through. A tool that is not a
/// function passes through unchanged — a surface returns <see cref="AITool"/>s and only a
/// function is invoked here.
/// <para>
/// A service rather than a static helper: it uses the ambient accessor itself, and a static
/// that needs a collaborator is a service every caller has to hand its dependency to.
/// </para>
/// </summary>
public sealed class TurnActivityTools(ITurnActivityObserverAccessor activity)
{
    public IList<AITool> Reporting(IList<AITool> tools)
    {
        ArgumentNullException.ThrowIfNull(tools);
        return [.. tools.Select(tool => tool is AIFunction f
            ? new ActivityReportingAIFunction(f, activity)
            : tool)];
    }
}
