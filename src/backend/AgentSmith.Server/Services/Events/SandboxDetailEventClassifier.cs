using AgentSmith.Contracts.Events;

namespace AgentSmith.Server.Services.Events;

/// <summary>
/// p0367: pure routing classifier. The Run SignalR group is a MEANING channel a
/// human watches (steps, LLM calls, decisions, verdicts); the per-sandbox group
/// is the DETAIL channel opened on demand via ExpandSandbox. High-frequency
/// per-tool-call events (SandboxCommand/SandboxResult) and the stdout stream
/// (SandboxOutput) belong ONLY to the detail channel — routing them here keeps a
/// run-detail view without an open drawer at O(steps), not O(tool-calls).
/// </summary>
public sealed class SandboxDetailEventClassifier
{
    private static readonly IReadOnlySet<EventType> DetailOnly = new HashSet<EventType>
    {
        EventType.SandboxCommand,
        EventType.SandboxResult,
        EventType.SandboxOutput,
    };

    public bool IsSandboxDetailOnly(EventType type) => DetailOnly.Contains(type);
}
