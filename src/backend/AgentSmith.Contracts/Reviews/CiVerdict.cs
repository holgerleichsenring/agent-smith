namespace AgentSmith.Contracts.Reviews;

/// <summary>
/// 2026-11-17-3435a: the one rule every provider decides a head by. Where the host marks checks required, only those
/// decide, so a failing optional check never starts a fix; any failed check is red even while others run, so a fix
/// starts on the first evidence; else anything running is pending; else any check is green; no check is none.
/// </summary>
public static class CiVerdict
{
    public static string Of(IReadOnlyList<CiCheck> checks)
    {
        ArgumentNullException.ThrowIfNull(checks);
        var deciding = Deciding(checks);
        if (deciding.Count == 0) return CiStates.None;
        if (deciding.Any(c => c.Outcome == CiCheckOutcome.Failed)) return CiStates.Red;
        return deciding.Any(c => c.Outcome == CiCheckOutcome.Pending) ? CiStates.Pending : CiStates.Green;
    }

    /// <summary>The checks the verdict was taken over.</summary>
    public static IReadOnlyList<CiCheck> Deciding(IReadOnlyList<CiCheck> checks) =>
        checks.Any(c => c.Required) ? [.. checks.Where(c => c.Required)] : checks;

    /// <summary>The newest time a deciding check reported; null when none did.</summary>
    public static DateTimeOffset? At(IReadOnlyList<CiCheck> checks) => Deciding(checks).Max(c => c.At);
}
