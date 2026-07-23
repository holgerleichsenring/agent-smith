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
/// </summary>
public sealed class ProgressLedgerToolHost : IToolHost
{
    private List<ProgressLedgerEntry> _entries;
    // p0356: awaited after every ACCEPTED replace — the mid-run durability
    // hook (ProgressLedgerFlusher publishes the ledger onto the event stream).
    // Awaited, not fire-and-forget, so a flush never outlives the tool call.
    private readonly Func<ProgressLedger, Task>? _onReplaced;
    private readonly ILogger _logger;

    public ProgressLedgerToolHost(
        IEnumerable<ProgressLedgerEntry>? seed = null,
        Func<ProgressLedger, Task>? onReplaced = null,
        ILogger? logger = null)
    {
        _entries = seed?.ToList() ?? new List<ProgressLedgerEntry>();
        _onReplaced = onReplaced;
        _logger = logger ?? NullLogger.Instance;
    }

    public ProgressLedger GetLedger() => new(_entries.AsReadOnly());

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

        // p0368: MERGE, don't replace — a DONE step survives a rewrite that drops or
        // regresses it (unless explicitly reopened). Pending work follows the incoming list.
        var merge = LedgerMergePolicy.Merge(GetLedger(), new ProgressLedger(mapped), reopened);
        WarnOnRescuedWork(merge);
        _entries = merge.Merged.Entries.ToList();
        if (_onReplaced is not null) await _onReplaced(GetLedger());
        return ProgressLedgerRenderer.Render(GetLedger());
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
