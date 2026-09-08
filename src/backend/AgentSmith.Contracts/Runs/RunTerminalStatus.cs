using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;

namespace AgentSmith.Contracts.Runs;

/// <summary>
/// p0439: the status a finished run is published with. A successful result is a
/// <see cref="RunStatuses.Success"/> unless the run delivered a shortfall — the executor
/// returns success for that too, because the delivery happened, and the status is what
/// tells the two apart.
/// </summary>
public static class RunTerminalStatus
{
    public static string Of(CommandResult result, PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(pipeline);
        if (!result.IsSuccess) return RunStatuses.Failed;
        return RunShortfall.DeliveredOn(pipeline) is null ? RunStatuses.Success : RunStatuses.Shortfall;
    }
}
