using System.Text;
using AgentSmith.Contracts.Commands;
using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// 2026-09-17-042ed: what the review of the proposal under revision found, rendered for the edit
/// turn that is about to re-emit it.
/// <para>
/// The confirmation the findings were shown in is POSTED to the thread, never appended to the
/// transcript, so a master told "address the finding" would be answering about something it has
/// never seen. The router seeds the proposal it is revising and the findings come with it.
/// </para>
/// </summary>
internal static class SpecDialogRevisionSection
{
    public static string Render(PipelineContext pipeline)
    {
        if (!pipeline.TryGet<OutcomeProposal>(ContextKeys.SpecDialogRevisedProposal, out var proposal)
            || proposal is null) return string.Empty;
        var sb = new StringBuilder();
        if (proposal.UnderReview) Interrupted(sb, proposal);
        if (proposal.Findings.Count == 0) return sb.ToString().TrimEnd();

        sb.AppendLine();
        sb.AppendLine("## What the review of your last proposal found");
        sb.AppendLine(
            "A fresh instance read the repositories above and reported the following against the "
            + "proposal you are revising. Each finding is the operator's to weigh — answer it in "
            + "your reply, by correcting the draft or by saying why it stands.");
        foreach (var finding in proposal.Findings)
            sb.AppendLine(
                $"- {finding.SpecId} — {finding.Problem}: {finding.Why}"
                + (finding.Quote is null ? string.Empty : $" (it states: \"{finding.Quote}\")")
                + (finding.Evidence is null ? string.Empty : $" (evidence: {finding.Evidence})"));
        return sb.ToString().TrimEnd();
    }

    // 2026-11-09-9a98: the turn that wrote this was stopped during its review, so neither the
    // transcript nor the thread holds it. Reproposed unchanged it keeps the rounds it already had.
    private static void Interrupted(StringBuilder sb, OutcomeProposal proposal)
    {
        sb.AppendLine();
        sb.AppendLine("## The proposal your last turn wrote");
        sb.AppendLine(
            "That turn was stopped while the proposal was being reviewed, so it never reached the operator "
            + "as an answer, though they can read it. Propose exactly this text again unless their message "
            + "changes it: what was already reviewed stays reviewed, and the rest is reviewed now.");
        foreach (var draft in ProposalSpecSet.Drafts(proposal))
        {
            sb.AppendLine("```yaml");
            sb.AppendLine(draft.Yaml);
            sb.AppendLine("```");
        }
    }
}
