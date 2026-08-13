namespace AgentSmith.Contracts.Models.Workers;

/// <summary>
/// p0416: which run, which step and which agent role a model call belongs to. Read from
/// the ambient run context at call time — the same attribution the LLM-call events carry
/// — so a worker (and every failure message) can say what it is answering.
/// </summary>
public sealed record WorkerCallIdentity(
    string? RunId,
    int? StepIndex,
    string? Role,
    string? Phase,
    string? Repo,
    string AgentType,
    string Model);
