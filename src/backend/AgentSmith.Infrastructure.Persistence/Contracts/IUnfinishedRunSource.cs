namespace AgentSmith.Infrastructure.Persistence.Contracts;

/// <summary>
/// 2026-08-24-ca23: the run ids the store still considers unfinished — running, queued or
/// parked on a question. A waiting run leaves the Redis active set, so cold-start
/// rehydration cannot see it there; without this it stays invisible to the drain until it
/// relaunches, and anything published against it meanwhile reaches the row but never the
/// trail or the fanout.
/// </summary>
public interface IUnfinishedRunSource
{
    Task<IReadOnlyList<string>> GetUnfinishedRunIdsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// 2026-10-02-5ab2c: the unfinished runs that are not parked on a question — the ones the
    /// broadcaster's periodic database discovery may track. A parked run with no stored
    /// position would be read from the start of its stream, so it is left to its relaunch.
    /// </summary>
    Task<IReadOnlyList<string>> GetUnparkedRunIdsAsync(CancellationToken cancellationToken);
}
