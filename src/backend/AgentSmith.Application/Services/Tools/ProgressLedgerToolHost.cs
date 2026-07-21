using System.ComponentModel;
using System.Text.Json;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Progress;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// p0341: hosts the <c>update_progress</c> tool — the coding master's durable
/// progress ledger (TodoWrite contract). Every call carries the COMPLETE item
/// list and FULLY REPLACES the store. p0359: the replace is fully model-owned —
/// the plan MAY deviate mid-run, so the master restructures the list freely
/// (add, reword, drop steps), exactly like an interactive harness's todo list.
/// The host keeps only the invariants that make the list coherent as MEMORY:
/// at most one in_progress and a size cap so the re-rendered ledger stays cheap.
/// Honesty is enforced downstream, not here: the keystone cross-checks whatever
/// the FINAL list claims against the actual diff (RunOutcomeKeystone).
/// The handler seeds it from the ratified plan and reads <see cref="GetLedger"/>
/// back into PipelineContext (the source of truth), mirroring LogDecisionToolHost.
/// </summary>
public sealed class ProgressLedgerToolHost : IToolHost
{
    private List<ProgressLedgerEntry> _entries;
    // p0356: awaited after every ACCEPTED full replace — the mid-run durability
    // hook (ProgressLedgerFlusher publishes the ledger onto the event stream).
    // Awaited, not fire-and-forget, so a flush never outlives the tool call.
    private readonly Func<ProgressLedger, Task>? _onReplaced;

    public ProgressLedgerToolHost(
        IEnumerable<ProgressLedgerEntry>? seed = null, Func<ProgressLedger, Task>? onReplaced = null)
    {
        _entries = seed?.ToList() ?? new List<ProgressLedgerEntry>();
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
        + "The checklist is yours: when the plan evolves, restructure it — add, reword, "
        + "or remove steps so the list always reflects the CURRENT plan. Keep it truthful; "
        + "the final list is cross-checked against the actual diff.")]
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

        _entries = mapped;
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
