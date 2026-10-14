namespace AgentSmith.Contracts.Decisions;

/// <summary>
/// 2026-10-13-8762p: a default a person decided — ratified or overruled — with who decided it, when, and the
/// note they left. The stored verdict is the act an overrule is; the decision file's copy is only a record.
/// </summary>
public sealed record DecidedDefault(
    long Id, string RunId, string? SpecId, string Question, string Chosen,
    string Verdict, string? Note, string By, DateTimeOffset At)
{
    /// <summary>The text that names this default in a decision file, so a copy is written once.</summary>
    public string Marker => $"[default #{Id}]";
}
