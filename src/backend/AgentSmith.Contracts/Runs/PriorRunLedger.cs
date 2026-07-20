namespace AgentSmith.Contracts.Runs;

/// <summary>
/// p0356: the latest same-ticket run's persisted progress ledger, read back for
/// the RESUME seed. StartedAt carries the prior run's start so the seeder can
/// age-cap the carry-over; Items are the persisted wire records (including the
/// p0356 Note field — the working state a resumed run needs).
/// </summary>
public sealed record PriorRunLedger(
    string RunId,
    DateTimeOffset StartedAt,
    IReadOnlyList<ProgressLedgerItemView> Items);
