using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.Tickets;

/// <summary>
/// One done criterion joined to an acceptance by its <see cref="CriterionKey"/>: the matching row's
/// verdict, or not_applicable with its reason when the master declined it, and the operator's overrule
/// of it from the judgements of the run that recorded the acceptance.
/// </summary>
public static class TicketSeriesCriterionJoin
{
    public static TicketSeriesCriterionView Of(
        string text, AcceptanceView? acceptance, IReadOnlyList<RunCriterionJudgement> judgements)
    {
        var key = CriterionKey.Of(text);
        var row = acceptance?.Criteria.FirstOrDefault(c => CriterionKey.Of(c.Text) == key);
        var declined = acceptance?.Declined?.FirstOrDefault(d => CriterionKey.Of(d.Text) == key);
        return new TicketSeriesCriterionView(
            text,
            row?.Status ?? (declined is null ? null : AcceptanceCriterionStatuses.NotApplicable),
            row?.Reason ?? declined?.Reason,
            row?.Citation,
            Overrule(key, judgements));
    }

    /// <summary>Every row and declined entry of the acceptance, as criteria in their own words.</summary>
    public static IReadOnlyList<TicketSeriesCriterionView> All(
        AcceptanceView acceptance, IReadOnlyList<RunCriterionJudgement> judgements) =>
        [
            .. acceptance.Criteria.Select(c => new TicketSeriesCriterionView(
                c.Text, c.Status, c.Reason, c.Citation, Overrule(CriterionKey.Of(c.Text), judgements))),
            .. (acceptance.Declined ?? []).Select(d => new TicketSeriesCriterionView(
                d.Text, AcceptanceCriterionStatuses.NotApplicable, d.Reason, null, Overrule(CriterionKey.Of(d.Text), judgements))),
        ];

    private static TicketCriterionOverrule? Overrule(string key, IReadOnlyList<RunCriterionJudgement> judgements) =>
        judgements.LastOrDefault(j => j.CriterionKey == key) is { } judged
            ? new TicketCriterionOverrule(judged.HumanStatus, judged.Reason, judged.Author, judged.RecordedAt)
            : null;
}
