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
/// </summary>
internal static class AcceptanceSnapshot
{
    /// <summary>
    /// The account the GATE decided on wins, where there is one.
    /// <para>
    /// The page used to be built only from a negotiated expectation and the master's own
    /// dispositions, while the gate has refused runs on the phase spec's criteria since
    /// p0393a. A live run showed both at once: the failure named three ratified criteria
    /// and the card said "No ratified acceptance contract on this run yet". Nothing was
    /// missing from the run — the page was reading the wrong one of two judges.
    /// </para>
    /// </summary>
    public static string? Build(
        RatifiedExpectation? expectation, MasterVerification? verification,
        RunAccounts? accounts)
    {
        var accounted = FromAccounts(accounts);
        if (accounted is not null) return accounted;
        if (expectation is null) return null;

        var dispositions = verification?.AcceptanceDispositions;
        var criteria = expectation.Draft.Expected
            .Select((text, i) => CriterionOf(text, i < dispositions?.Count ? dispositions![i] : null))
            .ToList();

        return RunStoryJson.Serialize(new AcceptanceView(
            criteria, expectation.Outcome, expectation.RatifiedBy,
            AcceptanceSources.MasterVerification));
    }

    /// <summary>
    /// Every criterion the run's accounts carry, with what it was decided on. A criterion the
    /// account could not take at all — a red build, a diff that would not run — is "unproven"
    /// and says why, because an unmeasured criterion and a failed one are different facts.
    /// </summary>
    private static string? FromAccounts(RunAccounts? accounts)
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

        return RunStoryJson.Serialize(new AcceptanceView(
            criteria, ExpectationOutcomes.Verbatim, RatifiedByThePhaseSpec,
            AcceptanceSources.DeliveryAccount));
    }

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

    private static AcceptanceCriterionView CriterionOf(string text, AcceptanceDisposition? disposition)
    {
        if (disposition is null)
            return new AcceptanceCriterionView(text, AcceptanceCriterionStatuses.Unproven, null);

        var status = disposition.Status switch
        {
            AcceptanceStatus.Met => AcceptanceCriterionStatuses.Met,
            AcceptanceStatus.NotApplicable => AcceptanceCriterionStatuses.NotApplicable,
            _ => AcceptanceCriterionStatuses.Unmet,
        };
        var reason = string.IsNullOrWhiteSpace(disposition.Evidence) ? null : disposition.Evidence;
        return new AcceptanceCriterionView(text, status, reason);
    }
}
