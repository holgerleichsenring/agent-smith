using System.Text.Json.Serialization;

namespace AgentSmith.Contracts.Runs;

/// <summary>
/// p0344b: the run's acceptance contract as served on the run detail — the
/// ratified criteria (p0328) paired with the master's per-criterion dispositions
/// (p0340), snapshotted at run end. Criterion status vocabulary: "met" | "unmet"
/// | "not_applicable" | "unproven" ("unproven" = the criterion was ratified but
/// the master reported no disposition for it). Outcome/RatifiedBy carry the
/// ratification facts (verbatim / edited / unratified, and who ratified).
/// <para>
/// 2026-10-12-6bfad: an account nobody could take — a red build, a diff that would not
/// run — is not a criterion and is not counted as one; it is listed in
/// <see cref="Unmeasured"/>. Snapshots archived before that wrote it as an "unproven" row
/// named after the repository, and under the delivery account that is the ONLY way an
/// unproven row arose (its criteria map to met, not_applicable or unmet). Such a row is
/// lifted here, on construction, so every reader of a stored snapshot sees one shape. A
/// later <c>with { Criteria = … }</c> bypasses the lift; nothing writes one.
/// </para>
/// </summary>
/// <param name="Source">2026-08-25-7f5a: which judge decided these — see
/// <see cref="AcceptanceSources"/>. Null on every run written before the distinction.</param>
public sealed record AcceptanceView(
    IReadOnlyList<AcceptanceCriterionView> Criteria,
    string Outcome,
    string RatifiedBy,
    string? Source = null,
    // 2026-09-06-3d81: what the master declined, beside the rows the gate decided on.
    IReadOnlyList<DeclinedCriterionView>? Declined = null,
    IReadOnlyList<UnmeasuredAccountView>? Unmeasured = null)
{
    // Declared in the body, so it would serialize after the positional members; the wire has
    // always led with the criteria.
    [JsonPropertyOrder(-1)]
    public IReadOnlyList<AcceptanceCriterionView> Criteria { get; init; } =
        [.. Criteria.Where(c => !IsAccountRow(c, Source))];

    public IReadOnlyList<UnmeasuredAccountView> Unmeasured { get; init; } =
    [
        .. Unmeasured ?? [],
        .. Criteria.Where(c => IsAccountRow(c, Source)).Select(c => new UnmeasuredAccountView(c.Text, c.Reason)),
    ];

    private static bool IsAccountRow(AcceptanceCriterionView criterion, string? source) =>
        source == AcceptanceSources.DeliveryAccount
        && criterion.Status == AcceptanceCriterionStatuses.Unproven;
}
