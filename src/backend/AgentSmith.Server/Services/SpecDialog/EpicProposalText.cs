using System.Text;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>The confirmation text of an epic: the parent, then each slice in order with what it requires.</summary>
public static class EpicProposalText
{
    public static string Describe(EpicOutcome epic, SpecDialogMarkup m)
    {
        ArgumentNullException.ThrowIfNull(epic);
        ArgumentNullException.ThrowIfNull(m);
        var sb = new StringBuilder();
        sb.AppendLine($"Proposed outcome: {m.Bold("epic")} `{epic.Parent.SpecId}` — {epic.Parent.Goal}");
        sb.AppendLine("Slices, in order:");
        foreach (var child in epic.Children)
            sb.AppendLine($"- `{child.SpecId}` {child.Goal}{FormatRequires(child)}");
        return sb.ToString().TrimEnd();
    }

    private static string FormatRequires(SpecDraft draft) =>
        draft.Requires.Count == 0 ? string.Empty : $" (requires: {string.Join(", ", draft.Requires)})";
}
