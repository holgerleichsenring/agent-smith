using System.Text;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-11-12-cd4cf: the open questions a proposal carries, for a chat confirmation — the card's questions in text, so
/// state_open_questions is their one channel on every surface and the reply need not repeat them.
/// </summary>
internal static class SpecDialogQuestionLines
{
    public static string Render(OutcomeProposal proposal, SpecDialogMarkup m)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        if (proposal.Questions.Count == 0) return string.Empty;
        var sb = new StringBuilder().AppendLine(m.Bold("Open questions:"));
        for (var i = 0; i < proposal.Questions.Count; i++)
        {
            var q = proposal.Questions[i];
            var specs = q.SpecIds.Count == 0 ? string.Empty : $" ({string.Join(", ", q.SpecIds)})";
            sb.AppendLine($"{i + 1}. {q.Question}{specs} — proposed: {q.Proposal}");
        }
        return sb.ToString();
    }
}
