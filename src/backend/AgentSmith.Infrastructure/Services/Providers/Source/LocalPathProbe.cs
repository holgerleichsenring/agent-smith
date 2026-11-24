namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-11-23-c81ea: whether a path is in a commit of a local repository — <c>git ls-tree &lt;rev&gt; -- &lt;path&gt;</c>,
/// which names the path when the commit holds it and answers nothing when it does not. Any failure of git itself
/// throws, so the branch write fails rather than drop a required deletion. (<c>cat-file -e</c> was the first choice; its
/// refusal wording differs by whether the working tree holds the path, so an absent path could not be told apart
/// from a failure without reading a sentence.) Not sealed: a test overrides the probe to move the branch between the
/// write's read of the head and its ref update, the one moment a real race can be staged against real git.
/// </summary>
public class LocalPathProbe
{
    public virtual async Task<bool> ExistsAtAsync(string repoPath, string? revision, string path, CancellationToken ct)
    {
        if (revision is null) return false;
        var result = await LocalGitCommand.RunAsync(repoPath, null, null, ct, "ls-tree", "--name-only", revision, "--", path);
        if (result.Exit != 0) throw new InvalidOperationException(result.Reason("ls-tree"));
        return result.Out.Split('\n', StringSplitOptions.TrimEntries).Contains(path.Trim('/'), StringComparer.Ordinal);
    }
}
