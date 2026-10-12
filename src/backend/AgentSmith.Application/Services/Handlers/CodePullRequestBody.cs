using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// The body of a coding run's pull request, after the banner the caller chose.
/// <para>
/// p0328/p0393a: the acceptance contract renders as a reviewer checklist. p0393a adds the
/// per-phase table and the discarded list — for a stopped sequence the table is the statement
/// that the repository is half migrated, spelled out phase by phase instead of implied by a red
/// check. p0429a: and the account the gate judged the run by, itemised. 2026-09-06-3d81: and the
/// criteria the master declined, with their reasons. 2026-09-24-b3c1: the pull-request body is
/// markdown, and an Azure DevOps description is HTML — rendered raw, a reviewer read the tags.
/// </para>
/// <para>
/// 2026-10-12-3a36c: its own type because CommitAndPRHandler composes it twice — for the PRs it
/// opens and for the spec PR a failed run keeps — and sits over its file-length baseline.
/// </para>
/// </summary>
internal static class CodePullRequestBody
{
    public static string Compose(
        CommitAndPRContext context, string banner, SpecSequenceProgress? progress, RunShortfall? shortfall) =>
        $"{banner}{TicketHtmlConverter.ToText(context.Ticket.Description)}"
        + $"{SpecPrBodySection.Build(context.Pipeline, progress, shortfall)}"
        + $"{RunAccountSection.Build(context.Pipeline)}"
        + $"{PhaseReviewSection.Build(context.Pipeline)}"
        + $"{DeclinedCriteriaSection.Build(context.Pipeline)}\n\n{Rework.ReworkRule.PrBodyLine}\n\n{SpecPrBody.SiblingMarker}";
}
