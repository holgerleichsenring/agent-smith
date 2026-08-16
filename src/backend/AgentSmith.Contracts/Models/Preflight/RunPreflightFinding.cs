namespace AgentSmith.Contracts.Models.Preflight;

/// <summary>
/// p0428: one per-run preflight check's answer. <see cref="Message"/> states what was
/// observed and is always secret-free; <see cref="Lever"/> names what the operator has
/// to CHANGE and is mandatory on a failure — "/root is not writable, give the sandbox a
/// writable home" is actionable where "IOException" is a symptom to reproduce.
/// </summary>
public sealed record RunPreflightFinding(
    string Check,
    RunPreflightVerdict Verdict,
    string Message,
    string? Lever = null)
{
    public static RunPreflightFinding Pass(string check, string message) =>
        new(check, RunPreflightVerdict.Pass, message);

    public static RunPreflightFinding Warn(string check, string message) =>
        new(check, RunPreflightVerdict.Warn, message);

    public static RunPreflightFinding Fail(string check, string message, string lever) =>
        new(check, RunPreflightVerdict.Fail, message, lever);

    /// <summary>One line for the run record: what is wrong, then the lever.</summary>
    public string Describe() =>
        string.IsNullOrEmpty(Lever) ? $"{Check}: {Message}" : $"{Check}: {Message} — {Lever}";
}
