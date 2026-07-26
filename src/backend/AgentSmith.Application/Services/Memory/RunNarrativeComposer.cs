using System.Text;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Memory;

/// <summary>
/// p0380: composes the run-side twin of state.done — ONE compact curated
/// `project` memory per ticket (what the run did/taught), deduped by ticket:
/// the entry name is derived from the ticket id, so a later green run on the
/// same ticket UPDATES the entry instead of appending per run (the p0287
/// raw-ledger bloat is exactly what this avoids).
/// </summary>
public static class RunNarrativeComposer
{
    private const int MaxListedFiles = 5;
    private const int MaxListedDecisions = 3;

    public static MemoryEntry Compose(
        Ticket ticket, string runId, IReadOnlyList<CodeChange> changes,
        IReadOnlyList<PlanDecision>? decisions)
    {
        var name = $"ticket-{MemorySlug.ToKebab(ticket.Id.Value)}";
        var description = $"{Truncate(ticket.Title, 80)} — last green run {runId}";
        return new MemoryEntry(name, description, MemoryEntryType.Project, ComposeBody(ticket, runId, changes, decisions));
    }

    private static string ComposeBody(
        Ticket ticket, string runId, IReadOnlyList<CodeChange> changes,
        IReadOnlyList<PlanDecision>? decisions)
    {
        var sourceFiles = changes
            .Select(c => c.Path.ToString())
            .Where(p => !RunRecordPaths.IsRunRecordPath(p))
            .ToList();
        var sb = new StringBuilder();
        sb.AppendLine($"# Ticket {ticket.Id.Value}: {ticket.Title}");
        sb.AppendLine();
        sb.AppendLine($"- Last green run: {runId}");
        sb.AppendLine($"- Changed {sourceFiles.Count} source file(s)"
                      + (sourceFiles.Count == 0 ? "" : $": {string.Join(", ", sourceFiles.Take(MaxListedFiles))}")
                      + (sourceFiles.Count > MaxListedFiles ? ", …" : ""));
        AppendDecisions(sb, decisions);
        return sb.ToString();
    }

    private static void AppendDecisions(StringBuilder sb, IReadOnlyList<PlanDecision>? decisions)
    {
        if (decisions is not { Count: > 0 }) return;
        sb.AppendLine("- Key decisions:");
        foreach (var d in decisions.Take(MaxListedDecisions))
            sb.AppendLine($"  - [{d.Category}] {Truncate(d.Decision, 160)}");
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max].TrimEnd() + "…";
}
