namespace AgentSmith.Contracts.Events;

public enum EventType
{
    RunStarted = 0,
    RunFinished = 1,
    SandboxCreated = 2,
    SandboxDisposed = 3,
    StepStarted = 4,
    StepFinished = 5,
    DecisionLogged = 10,
    GateChecked = 11,
    TriageRoute = 12,
    LlmCallStarted = 13,
    LlmCallFinished = 14,
    SandboxCommand = 20,
    SandboxOutput = 21,
    SandboxResult = 22,
    ToolCall = 23,
    ToolResult = 24,
    L1StepDetail = 25,
    CatalogIssue = 30,
    // p0177: L2SubAgent events (60-range — separated from L2Orchestrator
    // so sub-agent specific records stay visually grouped on the wire).
    SubAgentSpawned = 60,
    SubAgentObservation = 61,
    SubAgentFinding = 62,
    SubAgentFileWritten = 63,
    SubAgentToolCall = 64,
    SubAgentCompleted = 65,
}
