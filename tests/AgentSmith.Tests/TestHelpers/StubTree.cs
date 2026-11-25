using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Tests.TestHelpers;

/// <summary>
/// 2026-11-25-9992b: one working tree a <see cref="StubSandbox"/> holds — what was staged and written there, who it is a
/// clone of and where its head is. A workspace container serves every repository of a run, each at /work/&lt;repo&gt;,
/// so a stub container keeps one of these per tree root.
/// </summary>
internal sealed class StubTree(string root, string head)
{
    private const string WorkRoot = AgentSmith.Domain.Entities.Repository.WorkspaceRoot;
    private readonly string _rootPrefix = root.TrimEnd('/') + "/";

    public string Root => root;

    /// <summary>p0239: repo-relative writes — what `git add -A` in the tree would stage.</summary>
    public List<string> Staged { get; } = [];

    /// <summary>Written contents by repo-relative path, so a read of a written path returns what was written.</summary>
    public Dictionary<string, string> Written { get; } = new(StringComparer.Ordinal);

    public string? Origin { get; set; }

    public string Head { get; set; } = head;

    public int Commits { get; set; }

    /// <summary>A path relative to this tree's root; an absolute path elsewhere stays as it is.</summary>
    public string Normalize(string path) =>
        path.StartsWith(_rootPrefix, StringComparison.Ordinal) ? path[_rootPrefix.Length..] : path;

    /// <summary>
    /// The tree a step works in: the first two segments (/work/&lt;repo&gt;) of its absolute path, else of its working
    /// directory, else /work itself.
    /// </summary>
    public static string RootOf(Step step) =>
        UnderWork(step.Path) ?? UnderWork(step.WorkingDirectory) ?? WorkRoot;

    private static string? UnderWork(string? path)
    {
        if (path is null || !path.StartsWith(WorkRoot + "/", StringComparison.Ordinal)) return null;
        var repo = path[(WorkRoot.Length + 1)..].Split('/', 2)[0];
        return repo.Length == 0 ? null : $"{WorkRoot}/{repo}";
    }
}
