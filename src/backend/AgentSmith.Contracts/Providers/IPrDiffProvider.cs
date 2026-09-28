namespace AgentSmith.Contracts.Providers;

/// <summary>
/// Fetches PR/MR diff content from a source platform (read-only, no checkout).
/// </summary>
public interface IPrDiffProvider
{
    Task<PrDiff> GetDiffAsync(string prIdentifier, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents the diff of a pull request / merge request. HeadBranch and Author are the
/// branch the pull request comes from and who opened it, as the host reports them — what a
/// run started from a bare pull-request number needs to check the head out.
/// </summary>
public sealed record PrDiff(
    string BaseSha,
    string HeadSha,
    IReadOnlyList<ChangedFile> Files,
    string? HeadBranch = null,
    string? Author = null);

/// <summary>
/// A single file changed in a PR/MR diff.
/// </summary>
public sealed record ChangedFile(
    string Path,
    string Patch,
    ChangeKind Kind);

public enum ChangeKind
{
    Added,
    Modified,
    Deleted
}
