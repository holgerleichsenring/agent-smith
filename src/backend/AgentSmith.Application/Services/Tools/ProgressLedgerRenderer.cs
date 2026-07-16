using System.Text;
using AgentSmith.Contracts.Progress;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// p0341: THE canonical text form of a <see cref="ProgressLedger"/> — used for the
/// update_progress tool-result echo, the seed section rendered into the master
/// prompt, and the re-drive nudges. One renderer everywhere so the model sees a
/// consistent checklist. Pure, no I/O.
/// </summary>
public static class ProgressLedgerRenderer
{
    public static string Render(ProgressLedger ledger)
    {
        if (ledger.IsEmpty)
            return "Progress ledger: (empty) — seed it from your plan with update_progress.";
        var sb = new StringBuilder();
        sb.AppendLine(
            "Progress ledger (keep current with update_progress — full-state replace, one in_progress):");
        foreach (var e in ledger.Entries)
            sb.AppendLine($"- [{Mark(e.Status)}] {e.Id}. {e.Activity}{RenderTarget(e)}{RenderNote(e)}");
        return sb.ToString().TrimEnd();
    }

    private static string Mark(ProgressStatus status) => status switch
    {
        ProgressStatus.Done => "x",
        ProgressStatus.InProgress => "~",
        _ => " ",
    };

    private static string RenderTarget(ProgressLedgerEntry e) =>
        string.IsNullOrWhiteSpace(e.Target) ? string.Empty : $" (target: {e.Target})";

    private static string RenderNote(ProgressLedgerEntry e) =>
        string.IsNullOrWhiteSpace(e.Note) ? string.Empty : $" — {e.Note}";
}
