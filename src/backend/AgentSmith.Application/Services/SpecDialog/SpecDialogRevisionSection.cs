using System.Text;
using AgentSmith.Contracts.Commands;
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
            || proposal is not { Findings.Count: > 0 }) return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("## What the review of your last proposal found");
        sb.AppendLine(
            "A fresh instance read the repositories above and reported the following against the "
            + "proposal you are revising. Each finding is the operator's to weigh — answer it in "
            + "your reply, by correcting the draft or by saying why it stands.");
        foreach (var finding in proposal.Findings)
            sb.AppendLine(
                $"- {finding.PhaseId} — {finding.Problem}: {finding.Why}"
                + (finding.Quote is null ? string.Empty : $" (it states: \"{finding.Quote}\")")
                + (finding.Evidence is null ? string.Empty : $" (evidence: {finding.Evidence})"));
        return sb.ToString().TrimEnd();
    }
}
