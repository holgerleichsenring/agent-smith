using System.ComponentModel;
using System.Text.Json;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Progress;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// p0341: hosts the <c>update_progress</c> tool — the coding master's durable
/// progress ledger (TodoWrite contract). Every call carries the COMPLETE item
/// list. p0359 made the replace fully model-owned; p0368 reins that in for
/// COMPLETED work only: the incoming list is MERGED with the retained ledger via
/// <see cref="LedgerMergePolicy"/> so a DONE step stays DONE — a rewrite may still
/// restructure PENDING work freely (add, reword, drop, reorder), but it may not
/// silently discard (omit) or plain-revert a completed item. A done item leaves
/// DONE only via an EXPLICIT reopen status token. The host keeps the coherence
/// invariants (at most one in_progress, a size cap) and logs a warning whenever
/// the merge had to rescue completed work. Honesty is still enforced downstream:
/// the keystone cross-checks whatever the FINAL list claims against the diff.
/// The handler seeds it from the ratified plan and reads <see cref="GetLedger"/>
/// back into PipelineContext (the source of truth), mirroring LogDecisionToolHost.
/// <para>
/// p0374a restores the merge p0374 unwired and pays the price p0374 promised and
/// never shipped: every accepted update emits its <see cref="LedgerTransition"/>s
/// (entry, from-state, to-state, cause, master pass) and a refused rewrite is
/// NAMED in the tool's own reply instead of being silently kept. p0374's
/// rationale — the model must be able to correct its own record — is exactly what
/// the reopen token already covers, so the guard costs the model nothing it is
/// entitled to.
/// </para>
/// </summary>
public sealed class ProgressLedgerToolHost : IToolHost
{
    private List<ProgressLedgerEntry> _entries;
    // p0356: awaited after every ACCEPTED replace — the mid-run durability
    // hook (ProgressLedgerFlusher publishes the ledger onto the event stream).
    // Awaited, not fire-and-forget, so a flush never outlives the tool call.
    // p0374a: it also carries what CHANGED, which the overwritten snapshot cannot.
    private readonly Func<ProgressLedger, IReadOnlyList<LedgerTransition>, Task>? _onReplaced;
    // p0374a: the master's current re-engagement pass, read at update time — the
    // handler owns the loop counter, the host only stamps it onto the record.
    private readonly Func<int>? _currentPass;
    private readonly List<LedgerTransition> _transitions = [];
    private readonly ILogger _logger;

    public ProgressLedgerToolHost(
        IEnumerable<ProgressLedgerEntry>? seed = null,
        Func<ProgressLedger, IReadOnlyList<LedgerTransition>, Task>? onReplaced = null,
        ILogger? logger = null,
        Func<int>? currentPass = null)
    {
        _entries = seed?.ToList() ?? new List<ProgressLedgerEntry>();
        _onReplaced = onReplaced;
        _logger = logger ?? NullLogger.Instance;
        _currentPass = currentPass;
    }

    public ProgressLedger GetLedger() => new(_entries.AsReadOnly());

    /// <summary>p0374a: every transition this host recorded, in order — the run's
    /// ledger history as opposed to its current state.</summary>
    public IReadOnlyList<LedgerTransition> GetTransitions() => _transitions.AsReadOnly();

    public IEnumerable<AIFunction> GetTools(SkillExecutionPhase? phase, string? investigatorMode)
    {
        _ = phase;
        _ = investigatorMode;
        return [AIFunctionFactory.Create(UpdateProgress, name: "update_progress")];
    }

    [Description(
        "Replace the FULL progress checklist for this run. Pass the COMPLETE list every "
        + "time (full-state replacement, not a patch). Flip a step to in_progress before "
        + "working it and to done immediately after. At most one item may be in_progress. "
        + "The checklist is yours for PENDING work: when the plan evolves, restructure it — "
        + "add, reword, remove, or reorder steps so the list always reflects the CURRENT "
        + "plan. Completed work is preserved: a step already marked done STAYS done even if "
        + "you omit it or send it back as pending. To deliberately reopen a finished step "
        + "(e.g. a revised convention must be re-applied), give it the status 'reopen'. "
        + "Keep it truthful; the final list is cross-checked against the actual diff.")]
    public async Task<string> UpdateProgress(
        [Description("The complete checklist. Each item: id (stable across calls), activity, "
            + "status (pending|in_progress|done|reopen), optional target (repo-relative path "
            + "the step touches), optional note.")]
        IReadOnlyList<ProgressUpdateItem> items)
    {
        if (items is null) return "Error: items is required (pass the complete checklist).";
        if (items.Count > ProgressLedger.MaxItems)
            return $"Error: {items.Count} items exceeds the {ProgressLedger.MaxItems}-item cap — keep the checklist tight.";

        var mapped = new List<ProgressLedgerEntry>(items.Count);
        var reopened = new HashSet<string>(StringComparer.Ordinal);
        foreach (var i in items)
        {
            if (string.IsNullOrWhiteSpace(i.Id)) return "Error: every item needs a stable id.";
            if (!TryMapStatus(i.Status, out var status, out var isReopen))
                return $"Error: item '{i.Id}' has invalid status '{i.Status}' (use pending|in_progress|done|reopen).";
            if (i.Note is { Length: > ProgressLedger.MaxNoteLength })
                return $"Error: item '{i.Id}' note exceeds {ProgressLedger.MaxNoteLength} chars.";
            if (isReopen) reopened.Add(i.Id);
            mapped.Add(new ProgressLedgerEntry(i.Id, i.Activity ?? string.Empty, status, i.Target, i.Note));
        }

        if (mapped.Count(e => e.Status == ProgressStatus.InProgress) > 1)
            return "Error: at most one item may be in_progress at a time.";

        // p0368/p0374a: MERGE, don't replace — a DONE step survives a rewrite that drops
        // or regresses it, unless the model sends the explicit reopen token. Pending work
        // follows the incoming list, so the model's own plan stays its own.
        var merge = LedgerMergePolicy.Merge(
            GetLedger(), new ProgressLedger(mapped), reopened, _currentPass?.Invoke() ?? 0);
        WarnOnRescuedWork(merge);
        _entries = merge.Merged.Entries.ToList();
        _transitions.AddRange(merge.Transitions);
        if (_onReplaced is not null) await _onReplaced(GetLedger(), merge.Transitions);
        // p0374a: a refusal the model cannot see is a silent rewrite from where it
        // stands — it would read the returned checklist as its own and drift. The
        // reply names what was refused and how to reopen deliberately.
        return ProgressLedgerRenderer.Render(GetLedger()) + RefusalNotice(merge);
    }

    // p0374a: the refusal, stated to the model in the tool's own reply.
    private static string RefusalNotice(LedgerMergeResult merge)
    {
        if (!merge.RefusedAnything) return string.Empty;
        var parts = new List<string>(2);
        if (merge.ReattachedDone > 0)
            parts.Add($"{merge.ReattachedDone} completed step(s) you omitted were re-attached");
        if (merge.RejectedRegressions > 0)
            parts.Add($"{merge.RejectedRegressions} completed step(s) you sent back to pending were kept done");
        return "\n\nNote: " + string.Join("; ", parts)
            + ". Completed work only leaves done via the explicit 'reopen' status — "
            + "use it when a finished step genuinely has to be redone.";
    }

    private void WarnOnRescuedWork(LedgerMergeResult merge)
    {
        if (merge.ReattachedDone > 0)
            _logger.LogWarning(
                "update_progress tried to DISCARD {Count} completed step(s) by omission — "
                + "re-attached (a done step stays done).", merge.ReattachedDone);
        if (merge.RejectedRegressions > 0)
            _logger.LogWarning(
                "update_progress tried to REVERT {Count} completed step(s) to pending without a "
                + "'reopen' signal — kept done.", merge.RejectedRegressions);
    }

    private static bool TryMapStatus(string? raw, out ProgressStatus status, out bool isReopen)
    {
        isReopen = false;
        switch (raw?.Trim().ToLowerInvariant())
        {
            case "pending" or "todo" or "open": status = ProgressStatus.Pending; return true;
            case "in_progress" or "in-progress" or "inprogress" or "active":
                status = ProgressStatus.InProgress; return true;
            case "done" or "complete" or "completed": status = ProgressStatus.Done; return true;
            case "reopen" or "reopened" or "revert" or "reverted":
                status = ProgressStatus.Pending; isReopen = true; return true;
            default: status = ProgressStatus.Pending; return false;
        }
    }
}

/// <summary>p0341: the wire shape the model sends to update_progress (one item).
/// Distinct from <see cref="ProgressLedgerEntry"/> so the tool schema stays a
/// plain string status the model can emit; mapping + validation happen in the host.</summary>
public sealed record ProgressUpdateItem(
    string Id,
    string Activity,
    string Status,
    string? Target = null,
    string? Note = null);
