using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Contracts.Decisions;

/// <summary>
/// Logs architectural, tooling, and implementation decisions.
/// Implementation determines the target (file, in-memory, etc.) — callers always tell, never ask.
/// </summary>
public interface IDecisionLogger
{
    /// <summary>
    /// Records one decision. <paramref name="repositoryFiles"/> is the TARGET REPOSITORY's file
    /// surface — the sandbox the run checked it out into — not a path on the host running this
    /// process: the repository root is the sandbox mount <c>/work</c> (Repository.SandboxWorkPath),
    /// which exists only inside that sandbox. Null for a caller that has no repository in hand; the
    /// decision is then mirrored to the run's event stream only.
    /// </summary>
    Task<DecisionLogOutcome> LogAsync(ISandboxFileReader? repositoryFiles, DecisionCategory category,
                                      string decision,
                                      CancellationToken cancellationToken = default,
                                      string? sourceLabel = null);
}

public enum DecisionCategory
{
    Architecture,
    Tooling,
    Implementation,
    TradeOff
}

/// <summary>
/// 2026-09-19-c511b: which of the two places a decision reached. The run's event stream is the
/// record the dashboard reads; the repository file is a copy of it that travels with the code. They
/// fail separately and a caller that reports to the model needs to tell them apart — a lost copy
/// still leaves the decision recorded, a lost mirror leaves it recorded nowhere.
/// </summary>
public enum DecisionLogOutcome
{
    /// <summary>Mirrored to the run, and the repository copy written or not asked for.</summary>
    Recorded,

    /// <summary>Mirrored to the run; the repository copy could not be written (warned about).</summary>
    RecordedWithoutRepositoryCopy,

    /// <summary>Not recorded anywhere — the mirror itself failed.</summary>
    NotRecorded
}
