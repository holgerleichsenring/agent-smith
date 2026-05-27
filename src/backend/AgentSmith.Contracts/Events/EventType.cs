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
    ToolResult = 24
}
