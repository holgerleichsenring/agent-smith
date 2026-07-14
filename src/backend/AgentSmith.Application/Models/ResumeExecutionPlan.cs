using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Models;

/// <summary>p0327: the rehydrated step cursor — the commands a resumed run
/// re-enters at, plus the checkpointed execution count its step indices
/// continue from (one continuous run on the dashboard, not a restart).</summary>
public sealed record ResumeExecutionPlan(
    IReadOnlyList<PipelineCommand> Commands,
    int ExecutionCount);
