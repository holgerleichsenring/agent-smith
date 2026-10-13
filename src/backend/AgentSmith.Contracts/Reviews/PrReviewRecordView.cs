namespace AgentSmith.Contracts.Reviews;

/// <summary>2026-10-13-9a59b: one pull request's stored record.</summary>
/// <param name="PrKey"><see cref="Reviews.PrKey"/> of the URL.</param>
/// <param name="Url">The readable URL.</param>
/// <param name="State">One of <see cref="PrReviewStates"/>.</param>
/// <param name="StateAt">When the host says the state began; null before any read.</param>
/// <param name="RunId">The newest code run that names the pull request.</param>
/// <param name="LastEntryAt">The newest entry's time, derived on read.</param>
public sealed record PrReviewRecordView(
    string PrKey,
    string Url,
    string Repo,
    string Number,
    string Project,
    string TicketId,
    string RunId,
    string State,
    DateTimeOffset? StateAt,
    DateTimeOffset CheckedAt,
    DateTimeOffset? LastEntryAt,
    IReadOnlyList<PrReviewEntryView> Entries);
