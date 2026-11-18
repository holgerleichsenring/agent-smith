namespace AgentSmith.Contracts.Series;

/// <summary>2026-11-17-0207d: a series' acceptance criteria — the done criteria of the specs its tickets carry — and
/// the tickets whose specs could not be read, with why.</summary>
public interface ISeriesCriteria
{
    Task<(IReadOnlyList<SeriesCriterion> Criteria, IReadOnlyList<string> Unread)> ReadAsync(
        SeriesRunRecord record, Models.Configuration.ResolvedProject project, CancellationToken cancellationToken);
}
