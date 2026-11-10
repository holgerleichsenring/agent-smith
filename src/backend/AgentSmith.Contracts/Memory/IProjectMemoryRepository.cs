namespace AgentSmith.Contracts.Memory;

/// <summary>
/// 2026-11-10-fcd7a: the memories a project holds, keyed by the project's config entity. Where the composition
/// has no database (the CLI) the store is empty and says so on write: <see cref="IsAvailable"/> is false.
/// </summary>
public interface IProjectMemoryRepository
{
    bool IsAvailable { get; }

    /// <summary>The config entity id of the project named <paramref name="project"/>; null when no row exists.</summary>
    Task<long?> ResolveProjectAsync(string project, CancellationToken cancellationToken);

    /// <summary>Every memory of the project, newest first.</summary>
    Task<IReadOnlyList<ProjectMemoryRecord>> ListAsync(long projectId, CancellationToken cancellationToken);

    /// <summary>
    /// Writes the memory by slug, replacing an existing one. Throws <see cref="ArgumentException"/> when the text
    /// is over <see cref="ProjectMemoryLimits"/>, and <see cref="InvalidOperationException"/> where no store exists.
    /// </summary>
    Task UpsertAsync(long projectId, ProjectMemoryRecord record, CancellationToken cancellationToken);

    /// <summary>Removes the memory and its ledger rows, so a re-created slug starts over. False when none existed.</summary>
    Task<bool> DeleteAsync(long projectId, string slug, CancellationToken cancellationToken);
}
