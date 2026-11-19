using System.Text;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.SpecDialog.Writes;

/// <summary>
/// 2026-11-12-cd4ca: the draft the turn starts from — the proposal awaiting a decision. 2026-11-15-06fc: as its files,
/// each under the draft: path the file tools change it by. Empty when the turn starts with no draft.
/// </summary>
public static class ProposalDraftSection
{
    public static string Render(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (!pipeline.TryGet<SpecDialogReplySlot>(ContextKeys.SpecDialogReplySlot, out var slot)
            || slot is null || slot.Proposal.Current.IsEmpty) return string.Empty;
        var sb = new StringBuilder().AppendLine().AppendLine("## Your draft")
            .AppendLine("The files your draft holds now. edit_file revises one in place, write_file adds or replaces one, "
                + "write_file with delete: true removes one; a turn that changes none proposes nothing new and this draft stands as it is.");
        foreach (var file in DraftFiles.Of(slot.Proposal.Current))
            sb.AppendLine($"### {DraftPath.Shown(file.Path)}").AppendLine(Fence(file.Path))
                .AppendLine(file.Text.TrimEnd()).AppendLine("```");
        return sb.ToString().TrimEnd();
    }

    private static string Fence(string path) => path.EndsWith(".yaml", StringComparison.Ordinal) ? "```yaml" : "```";
}
