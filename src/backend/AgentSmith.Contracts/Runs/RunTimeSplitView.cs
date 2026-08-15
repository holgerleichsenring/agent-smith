namespace AgentSmith.Contracts.Runs;

/// <summary>
/// p0404: where a step's (or a run's) wall-clock went. Model time and sandbox
/// time are MEASURED per call; scaffolding is the SUBTRACTION that remains — the
/// framework moving data between the two. Naming the remainder is the point:
/// it is the part nobody owns, and it is invisible until it has a number.
/// <para>
/// <see cref="ThrottleMs"/> is a SUBSET of <see cref="ModelMs"/>, not a fourth
/// addend — the rate-limiter wait happens inside the measured call. So the sum
/// that reconstructs the duration is model + sandbox + scaffolding.
/// </para>
/// </summary>
/// <param name="ScaffoldingMs">
/// Null while the duration it is subtracted from is unknown (a step still
/// running) — an unfinished step reports no remainder rather than a zero that
/// reads as "no scaffolding".
/// </param>
public sealed record RunTimeSplitView(
    long ModelMs,
    long ThrottleMs,
    long SandboxMs,
    long? ScaffoldingMs)
{
    /// <summary>
    /// Composes the split for a segment whose measured parts are known and whose
    /// wall-clock may not be. Clamped at zero: measured parts can slightly exceed
    /// a rounded duration, and a negative remainder is noise, not a finding.
    /// </summary>
    public static RunTimeSplitView From(
        long modelMs, long throttleMs, long sandboxMs, double? durationSeconds) =>
        new(modelMs, throttleMs, sandboxMs,
            durationSeconds is { } seconds
                ? Math.Max(0L, (long)(seconds * 1000) - modelMs - sandboxMs)
                : null);
}
