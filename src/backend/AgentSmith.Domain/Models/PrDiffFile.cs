namespace AgentSmith.Domain.Models;

/// <summary>
/// One file changed in a PR diff. Binary files (no textual patch on the
/// platform API) carry metadata only: IsBinary=true and an empty hunk list.
/// </summary>
public sealed record PrDiffFile(
    string Path,
    PrFileChangeKind Kind,
    bool IsBinary,
    IReadOnlyList<PrHunk> Hunks);
