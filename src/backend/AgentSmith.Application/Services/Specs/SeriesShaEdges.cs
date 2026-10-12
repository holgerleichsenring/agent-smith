using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-12-f5f7b: compares the last commit per repository the set was read at against the shas
/// this system recorded. A repository read now whose sha differs — or which the map does not hold —
/// MOVED, and any move is a reviewer's edit. A repository the map holds that this read did not reach
/// (not checked out, or no longer a member) is NOT compared; the caller logs it.
/// </summary>
public static class SeriesShaEdges
{
    /// <summary>True when any repository the set was read from moved since this system wrote it.</summary>
    public static bool Moved(SpecSetReadResult previous, SpecSetPointer pointer)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(pointer);
        // A read that carries no per-repository shas compares the one sha, as it always did.
        if (previous.RevisionShas.Count == 0)
            return !string.Equals(pointer.RevisionSha, previous.LastCommitSha, StringComparison.Ordinal);
        var recorded = pointer.ShaByRepo;
        return previous.RevisionShas.Any(read =>
            !recorded.TryGetValue(read.Key, out var sha) || !string.Equals(sha, read.Value, StringComparison.Ordinal));
    }

    /// <summary>The repositories the pointer records that this read did not reach.</summary>
    public static IReadOnlyList<string> NotCompared(SpecSetReadResult? previous, SpecSetPointer? pointer) =>
        previous is null || pointer is null || previous.RevisionShas.Count == 0
            ? []
            : [.. pointer.ShaByRepo.Keys.Where(repo => !previous.RevisionShas.ContainsKey(repo))];
}
