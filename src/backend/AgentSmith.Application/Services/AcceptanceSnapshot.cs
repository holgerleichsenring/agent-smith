using AgentSmith.Contracts.Expectations;
using AgentSmith.Contracts.Runs;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services;

/// <summary>
/// 2026-08-25-7f5a: the run's acceptance dispositions as the run detail serves them, from
/// whichever judge actually decided.
/// <para>
/// Separated from <see cref="RunStorySnapshotBuilder"/> when it gained a second source: which
/// judge produced a verdict, and how that verdict is rendered as a row, is a different
/// question from how a run's story is snapshotted.
/// </para>
/// <para>
/// 2026-09-06-3d81: the criteria the master DECLINED ride beside the rows, whichever judge
/// the rows came from. The rows are what the gate decided on; a declined criterion is the
/// master's answer that no work here could make it true — side by side, never merged.
/// </para>
/// </summary>
internal static class AcceptanceSnapshot
{
    /// <summary>
    /// The account the GATE decided on wins, where there is one.
    /// <para>
    /// The page used to be built only from a negotiated expectation and the master's own
    /// dispositions, while the gate has refused runs on the phase spec's criteria since
    /// p0393a. A live run showed both at once: the failure named three ratified criteria
    /// and the card said "No ratified acceptance contract on this run yet". The negotiation
    /// is gone, so the gate's account is the one judge left; archived snapshots written from
    /// the negotiated source still decode, because the view shape did not change.
    /// </para>
    /// </summary>
    public static string? Build(RunAccounts? accounts, IReadOnlyList<DeclinedCriterion>? declined = null)
    {
        var declinedViews = DeclinedViews(declined);
        var view = FromAccounts(accounts);
        if (view is null)
            return declinedViews is null ? null : RunStoryJson.Serialize(DeclinedOnly(declinedViews));
        return RunStoryJson.Serialize(view with { Declined = declinedViews });
    }

    /// <summary>
    /// Every criterion the run's accounts carry, with what it was decided on. A criterion the
    /// account could not take at all — a red build, a diff that would not run — is "unproven"
    /// and says why, because an unmeasured criterion and a failed one are different facts.
    /// </summary>
    private static AcceptanceView? FromAccounts(RunAccounts? accounts)
    {
        var all = accounts?.All;
        if (all is not { Count: > 0 }) return null;

        var criteria = all
            .SelectMany(account => account.Criteria.Count > 0
                ? account.Criteria.Select(Row)
                : [new AcceptanceCriterionView(
                    account.RepoKey, AcceptanceCriterionStatuses.Unproven, account.Problem)])
            .ToList();
        if (criteria.Count == 0) return null;

        return new AcceptanceView(
            criteria, ExpectationOutcomes.Verbatim, RatifiedByThePhaseSpec, AcceptanceSources.DeliveryAccount);
    }

    /// <summary>A run that declined something and was judged by nothing else — it ended
    /// between the master's verdict and the account — still shows what it declined.</summary>
    private static AcceptanceView DeclinedOnly(IReadOnlyList<DeclinedCriterionView> declined) =>
        new([], ExpectationOutcomes.Verbatim, RatifiedByThePhaseSpec, AcceptanceSources.MasterVerification, declined);

    private static IReadOnlyList<DeclinedCriterionView>? DeclinedViews(IReadOnlyList<DeclinedCriterion>? declined) =>
        declined is not { Count: > 0 } ? null
            : [.. declined.Select(d => new DeclinedCriterionView(d.Criterion, d.Reason, d.PhaseId))];

    /// <summary>The phase spec IS the contract on this path — nobody edited it into one, so
    /// there is no person to name and saying so is more honest than borrowing a name.</summary>
    private const string RatifiedByThePhaseSpec = "the ratified phase spec";

    /// <summary>2026-08-25-9749: the account's third disposition reaches the page as the
    /// status the page already had a word for. The run detail has rendered not_applicable
    /// since the master's own dispositions carried it; the delivery account could not say
    /// it, and every declined criterion arrived as "unmet".</summary>
    private static AcceptanceCriterionView Row(CriterionAccount criterion) =>
        new(criterion.Criterion, Status(criterion.Disposition), criterion.Note, criterion.Citation);

    private static string Status(AccountDisposition disposition) => disposition switch
    {
        AccountDisposition.Satisfied => AcceptanceCriterionStatuses.Met,
        AccountDisposition.NotApplicable => AcceptanceCriterionStatuses.NotApplicable,
        _ => AcceptanceCriterionStatuses.Unmet,
    };
}
