using System.ComponentModel;
using System.Text.Json;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Progress;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// p0341: hosts the <c>update_progress</c> tool — the coding master's durable
/// progress ledger (TodoWrite contract). Every call carries the COMPLETE item
/// list and FULLY REPLACES the store; the host enforces the invariants that make
/// a full replace safe as MEMORY: at most one in_progress, no silent drop of a
/// seeded or already-done item (reconcile-by-id), and a size cap so the
/// re-rendered ledger stays cheap. It is not a gate — completion stays p0340's.
/// The handler seeds it from the ratified plan and reads <see cref="GetLedger"/>
/// back into PipelineContext (the source of truth), mirroring LogDecisionToolHost.
/// </summary>
public sealed class ProgressLedgerToolHost : IToolHost
{
    private List<ProgressLedgerEntry> _entries;
    // Ids that must never vanish from a full replace: the seeded plan steps and
    // any item ever marked done. The model may flip their status but not drop them.
    private readonly HashSet<string> _protectedIds;
    // p0356: awaited after every ACCEPTED full replace — the mid-run durability
    // hook (ProgressLedgerFlusher publishes the ledger onto the event stream).
    // Awaited, not fire-and-forget, so a flush never outlives the tool call.
    private readonly Func<ProgressLedger, Task>? _onReplaced;

    public ProgressLedgerToolHost(
        IEnumerable<ProgressLedgerEntry>? seed = null, Func<ProgressLedger, Task>? onReplaced = null)
    {
        _entries = seed?.ToList() ?? new List<ProgressLedgerEntry>();
        _protectedIds = new HashSet<string>(
            _entries.Where(e => e.Status == ProgressStatus.Done).Select(e => e.Id), StringComparer.Ordinal);
        foreach (var e in _entries) _protectedIds.Add(e.Id); // seeded ids are protected
        _onReplaced = onReplaced;
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
        + "Do not drop a seeded or already-done step — keep the list tight and current.")]
    public async Task<string> UpdateProgress(
        [Description("The complete checklist. Each item: id (stable across calls), activity, "
            + "status (pending|in_progress|done), optional target (repo-relative path the step "
            + "touches), optional note.")]
        IReadOnlyList<ProgressUpdateItem> items)
    {
        if (items is null) return "Error: items is required (pass the complete checklist).";
        if (items.Count > ProgressLedger.MaxItems)
            return $"Error: {items.Count} items exceeds the {ProgressLedger.MaxItems}-item cap — keep the checklist tight.";

        var mapped = new List<ProgressLedgerEntry>(items.Count);
        foreach (var i in items)
        {
            if (string.IsNullOrWhiteSpace(i.Id)) return "Error: every item needs a stable id.";
            if (!TryMapStatus(i.Status, out var status))
                return $"Error: item '{i.Id}' has invalid status '{i.Status}' (use pending|in_progress|done).";
            if (i.Note is { Length: > ProgressLedger.MaxNoteLength })
                return $"Error: item '{i.Id}' note exceeds {ProgressLedger.MaxNoteLength} chars.";
            mapped.Add(new ProgressLedgerEntry(i.Id, i.Activity ?? string.Empty, status, i.Target, i.Note));
        }

        if (mapped.Count(e => e.Status == ProgressStatus.InProgress) > 1)
            return "Error: at most one item may be in_progress at a time.";

        var newIds = new HashSet<string>(mapped.Select(e => e.Id), StringComparer.Ordinal);
        var dropped = _protectedIds.Where(id => !newIds.Contains(id)).ToList();
        if (dropped.Count > 0)
            return "Error: a full replace may not DROP a seeded or already-done step (that is how "
                + $"progress silently disappears). Missing: {string.Join(", ", dropped)}. Keep them "
                + "in the list (mark done with a note if genuinely not needed).";

        _entries = mapped;
        foreach (var e in mapped.Where(e => e.Status == ProgressStatus.Done)) _protectedIds.Add(e.Id);
        if (_onReplaced is not null) await _onReplaced(GetLedger());
        return ProgressLedgerRenderer.Render(GetLedger());
    }

    private static bool TryMapStatus(string? raw, out ProgressStatus status)
    {
        switch (raw?.Trim().ToLowerInvariant())
        {
            case "pending" or "todo" or "open": status = ProgressStatus.Pending; return true;
            case "in_progress" or "in-progress" or "inprogress" or "active":
                status = ProgressStatus.InProgress; return true;
            case "done" or "complete" or "completed": status = ProgressStatus.Done; return true;
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
