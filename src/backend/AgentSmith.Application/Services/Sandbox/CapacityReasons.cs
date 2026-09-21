using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// 2026-09-21-5c17: the sentences an admission refusal is carried in, written ONCE for both
/// admission doors — the spawn funnel's reservation and the manual init's admission. The copy
/// that used to live in each said "footprint N exceeds the remaining budget" whatever had
/// refused, so an operator on a host with no configured budget went looking for a limit that
/// did not exist.
/// <para>
/// The two gates are not symmetric and the text says so. The sandbox probe knows what it
/// refused against and explains itself, so its words are CARRIED. The capacity ledger answers
/// through a boolean contract and discards the quantities it compared, so its sentence is
/// COMPOSED here from the footprint — the one gate those numbers are actually true for.
/// </para>
/// </summary>
public static class CapacityReasons
{
    /// <summary>
    /// The capacity ledger's refusal. It leads with the footprint because that is the only
    /// quantity it holds: its contract returns a bool and the bound it compared against is
    /// gone by the time anyone can write a sentence about it.
    /// </summary>
    public static string LedgerFull(RunFootprintBreakdown footprint) =>
        $"footprint {footprint.TotalMemLimit} / {footprint.TotalCpuLimit} cpu "
        + "exceeds the remaining budget";

    /// <summary>
    /// A probe's own refusal on its way to a run row. The decision type allows a denial with a
    /// null reason, the queue entry's reason is non-nullable and the surface renders an empty
    /// element for an empty string — which is worse for an operator than a wrong sentence. No
    /// production probe denies wordlessly; this is the defence that keeps it that way.
    /// </summary>
    public static string Carried(string? reason) =>
        string.IsNullOrWhiteSpace(reason) ? "the sandbox host has no room for this run" : reason;
}
