using AgentSmith.Contracts.Models;

namespace AgentSmith.Contracts.Sandbox;

/// <summary>
/// 2026-11-04-96a0c: how much of one upload earlier turns of the conversation opened with read_file. Only the files
/// read_file can open count (<paramref name="Readable"/>: 1 MB or under, strict UTF-8); the rest are counted apart in
/// <paramref name="Unreadable"/>. <paramref name="Opened"/> holds only readable paths.
/// </summary>
public sealed record ReferenceCoverage(IReadOnlySet<string> Readable, IReadOnlySet<string> Opened, int Unreadable)
{
    /// <summary>How many unopened files the prompt lists by path.</summary>
    public const int UnopenedShown = 25;

    public static readonly ReferenceCoverage Empty = new(new HashSet<string>(), new HashSet<string>(), 0);

    /// <summary>M: the files read_file can open.</summary>
    public int Files => Readable.Count;

    public int UnopenedCount => Files - Opened.Count;

    /// <summary>The first <see cref="UnopenedShown"/> unopened readable paths, in path order.</summary>
    public IReadOnlyList<string> Unopened =>
        [.. Readable.Where(path => !Opened.Contains(path)).Order(StringComparer.Ordinal).Take(UnopenedShown)];

    /// <summary>The paths among <paramref name="paths"/> that are readable files of the set.</summary>
    public IReadOnlyList<string> ReadableOf(IEnumerable<string> paths) => [.. paths.Where(Readable.Contains).Distinct()];

    /// <summary>What a turn that opened <paramref name="thisTurn"/> read of the upload, after the merge.</summary>
    public UploadProvenance Stamp(string address, string setId, IReadOnlyList<string> thisTurn) =>
        new(address, setId, thisTurn.Count, Opened.Union(thisTurn).Count(), Files);
}
