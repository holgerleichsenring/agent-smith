using System;
using System.Collections.Generic;
using System.Linq;

namespace AgentSmith.Contracts.Progress;

/// <summary>
/// p0368: merges an incoming update_progress replacement with the retained ledger
/// so that a step which is DONE STAYS DONE. p0359 made the replace fully
/// model-owned (any restructure legal); that let a coding master wholesale-rewrite
/// its checklist and silently DISCARD already-completed items — the run then
/// re-treads finished work and ShouldReengage (keyed off the done-count /
/// actionable-pending) is driven forever. The rule is NOT append-only: PENDING
/// work stays freely editable (add, reword, drop, reorder). A DONE entry may only
/// leave DONE via an EXPLICIT reopen signal — never by omission or a plain
/// status regression in a rewrite. On every accepted update the retained DONE set
/// is preserved: matched-and-kept-done, omitted-and-re-attached, or
/// regressed-and-forced-back-to-done unless explicitly reopened.
/// </summary>
public static class LedgerMergePolicy
{
    // Joins the fallback match key's two halves; a control char never occurs in an
    // activity or a repo path, so it cannot collide across the boundary.
    private const string KeySeparator = "\u001F";

    /// <param name="explicitReopens">Ids the model deliberately reopened this call
    /// (an explicit, logged signal) — the only way a retained DONE step is allowed
    /// to regress to pending.</param>
    public static LedgerMergeResult Merge(
        ProgressLedger retained, ProgressLedger incoming, IReadOnlySet<string> explicitReopens)
    {
        var retainedDone = retained.Entries.Where(e => e.Status == ProgressStatus.Done).ToList();
        var byId = BuildIndex(retainedDone, e => e.Id);
        var byFallback = BuildIndex(retainedDone.Where(HasFallback), FallbackKey);
        var consumed = new HashSet<string>(StringComparer.Ordinal);

        var merged = new List<ProgressLedgerEntry>(incoming.Entries.Count + retainedDone.Count);
        int reattached = 0, rejected = 0, reverted = 0;

        foreach (var inc in incoming.Entries)
        {
            var match = FindRetainedDone(inc, byId, byFallback, consumed);
            if (match is null) { merged.Add(inc); continue; }

            consumed.Add(match.Id);
            if (inc.Status == ProgressStatus.Done) merged.Add(inc);
            else if (explicitReopens.Contains(inc.Id)) { merged.Add(inc); reverted++; }
            else { merged.Add(inc with { Status = ProgressStatus.Done }); rejected++; }
        }

        foreach (var done in retainedDone.Where(d => !consumed.Contains(d.Id)))
        {
            merged.Add(done);
            reattached++;
        }

        return new LedgerMergeResult(new ProgressLedger(merged), reattached, rejected, reverted);
    }

    private static ProgressLedgerEntry? FindRetainedDone(
        ProgressLedgerEntry inc,
        IReadOnlyDictionary<string, ProgressLedgerEntry> byId,
        IReadOnlyDictionary<string, ProgressLedgerEntry> byFallback,
        ISet<string> consumed)
    {
        if (byId.TryGetValue(inc.Id, out var m) && !consumed.Contains(m.Id)) return m;
        if (HasFallback(inc) && byFallback.TryGetValue(FallbackKey(inc), out var f)
            && !consumed.Contains(f.Id)) return f;
        return null;
    }

    private static Dictionary<string, ProgressLedgerEntry> BuildIndex(
        IEnumerable<ProgressLedgerEntry> entries, Func<ProgressLedgerEntry, string> key)
    {
        var index = new Dictionary<string, ProgressLedgerEntry>(StringComparer.Ordinal);
        foreach (var e in entries) index.TryAdd(key(e), e);
        return index;
    }

    // Fallback key so a REWORDED rewrite (new/unstable id) still matches the same
    // completed step: normalized activity + declared target.
    private static string FallbackKey(ProgressLedgerEntry e) =>
        Normalize(e.Activity) + KeySeparator + Normalize(e.Target);

    private static bool HasFallback(ProgressLedgerEntry e) =>
        !string.IsNullOrWhiteSpace(e.Activity) || !string.IsNullOrWhiteSpace(e.Target);

    private static string Normalize(string? s) =>
        string.IsNullOrWhiteSpace(s) ? string.Empty : s.Trim().ToLowerInvariant();
}

/// <summary>p0368: outcome of a ledger merge — the preserved ledger plus the
/// visibility counters the tool host logs (a rewrite that tried to discard or
/// revert completed work is surfaced to the operator).</summary>
public sealed record LedgerMergeResult(
    ProgressLedger Merged, int ReattachedDone, int RejectedRegressions, int ExplicitReverts);
