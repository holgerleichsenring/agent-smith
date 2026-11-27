using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Series;

/// <summary>2026-11-27-3532c: opens what the series master reads in a beat — its project's repositories at the default
/// branch, at the branch of each ticket a run or a pull request worked, and at the series branch, and its conversation's
/// uploads. The server supplies it; a composition without it gives no reads.</summary>
public interface ISeriesSourceScopes
{
    /// <summary>The beat's read-only scopes, lazy, owned by the caller and disposed when the beat ends.</summary>
    Task<SeriesSourceMap> OpenAsync(SeriesBeat beat, ResolvedProject project, CancellationToken cancellationToken);
}
