using AgentSmith.Contracts.Models.Configuration.Resolved;

namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// 2026-09-23-2446: the PROCESS-WIDE half of the sandbox hold window — the field, then
/// <see cref="EnvironmentVariable"/>, then <see cref="Default"/>, each clamped at zero —
/// in ONE place, because two callers now need the same answer: the scan-time resolver that
/// decides whether a labelled sandbox is a corpse, and the project form's inherited-value
/// projection that has to name the window the next scan will use.
/// <para>
/// It reports WHICH leg answered, not only what it answered. An installation whose catalog
/// says nothing still has a window, and telling an operator that came from the settings
/// form would send them looking for a value that is not there.
/// </para>
/// <para>
/// It lives in the contracts assembly because <see cref="Default"/> is a non-literal static
/// field on a static class, which the no-static-state rule flags in the application and
/// infrastructure assemblies. Contracts is outside that scan, so the chain costs no
/// allowlist entry — and an allowlist entry is the kind of exemption that makes the next
/// rule violation easier to argue for.
/// </para>
/// </summary>
public static class SandboxHoldWindow
{
    /// <summary>The empty-store fallback: read only when the catalog names no window.</summary>
    public const string EnvironmentVariable = "SANDBOX_HOLD_SECONDS";

    /// <summary>
    /// Three minutes: long enough to cover reading a reply and typing the next question,
    /// which is where a design conversation lives. A wrong value costs idle resources and
    /// never a refused run, because a hold is released before any capacity probe.
    /// </summary>
    public static readonly TimeSpan Default = TimeSpan.FromSeconds(180);

    /// <summary>
    /// The window a project that overrides nothing gets, and the leg that produced it.
    /// The built-in default is reported as a CODE default rather than a global one: no
    /// configuration holds it, so naming it after the settings form would be a lie in the
    /// same direction as naming the environment leg after it.
    /// </summary>
    public static ResolvedValue<int> ProcessWide(SandboxGlobalConfig? sandbox)
    {
        if (sandbox?.HoldSeconds is { } configured)
            return ResolvedValue<int>.Global(Clamped(configured));
        return int.TryParse(Environment.GetEnvironmentVariable(EnvironmentVariable), out var fromEnvironment)
            ? ResolvedValue<int>.FromEnvironment(Clamped(fromEnvironment))
            : ResolvedValue<int>.CodeDefault((int)Default.TotalSeconds);
    }

    /// <summary>
    /// Seconds as a window, clamped at zero — a negative window is not a shorter hold, it
    /// is nonsense, and zero already means hold nothing.
    /// </summary>
    public static TimeSpan Window(int seconds) => TimeSpan.FromSeconds(Clamped(seconds));

    private static int Clamped(int seconds) => Math.Max(0, seconds);
}
