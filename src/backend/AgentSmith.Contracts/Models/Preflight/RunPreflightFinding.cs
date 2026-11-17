using AgentSmith.Domain.Models;

namespace AgentSmith.Contracts.Models.Preflight;

/// <summary>
/// p0428: one per-run preflight check's answer. <see cref="Message"/> states what was
/// observed and is always secret-free; <see cref="Lever"/> names what the operator has
/// to CHANGE and is mandatory on a failure — "/root is not writable, give the sandbox a
/// writable home" is actionable where "IOException" is a symptom to reproduce.
/// <para>
/// 2026-11-16-f3fba: a failure may also name its <see cref="Kind"/> (a RunEndKinds value) and its
/// <see cref="Detail"/>; the gate carries the first failing check's pair to the run's end.
/// </para>
/// </summary>
public sealed record RunPreflightFinding(
    string Check,
    RunPreflightVerdict Verdict,
    string Message,
    string? Lever = null)
{
    /// <summary>2026-11-16-f3fba: why the run ends when this finding fails it; null reads as preflight.</summary>
    public string? Kind { get; init; }

    /// <summary>2026-11-16-f3fba: the refusal's subject and the setting that lifts it.</summary>
    public EndDetail? Detail { get; init; }

    public static RunPreflightFinding Pass(string check, string message) =>
        new(check, RunPreflightVerdict.Pass, message);

    public static RunPreflightFinding Warn(string check, string message, string? lever = null) =>
        new(check, RunPreflightVerdict.Warn, message, lever);

    public static RunPreflightFinding Fail(string check, string message, string lever) =>
        new(check, RunPreflightVerdict.Fail, message, lever);

    /// <summary>One line for the run record: what is wrong, then the lever.</summary>
    public string Describe() =>
        string.IsNullOrEmpty(Lever) ? $"{Check}: {Message}" : $"{Check}: {Message} — {Lever}";
}
