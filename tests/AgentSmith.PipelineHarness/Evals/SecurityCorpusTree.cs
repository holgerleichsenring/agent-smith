namespace AgentSmith.PipelineHarness.Evals;

/// <summary>
/// 2026-08-28-cc40: the corpus as a real working tree the scan can be pointed at.
/// <para>
/// Real files in a real git repository, because the scan READS. Three of the preset's
/// steps walk the tree through the sandbox and one of them walks the history; a simulated
/// filesystem answering a fixed set of reads would score whether the scan asks the
/// questions the fixture author expected, which is not the measurement wanted here.
/// </para>
/// </summary>
public sealed class SecurityCorpusTree : IAsyncDisposable
{
    private const string Branch = "main";

    private SecurityCorpusTree(string root) => Root = root;

    /// <summary>The directory the sandbox mounts as <c>/work</c>.</summary>
    public string Root { get; }

    public static async Task<SecurityCorpusTree> MaterialiseAsync(
        SecurityCorpus corpus, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(corpus);
        var root = Path.Combine(
            Path.GetTempPath(), "security-corpus-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(root);
        foreach (var file in corpus.Files) Write(root, file);

        await FixtureGit.InitAsync(root, Branch, ct);
        await FixtureGit.RunAsync(root, ct, "add", "-A");
        await FixtureGit.RunAsync(root, ct, "commit", "--quiet", "-m", "corpus");
        return new SecurityCorpusTree(root);
    }

    /// <summary>What the tree really holds, relative to its root — the check that a
    /// declaration nobody wrote to disk cannot be scored as a miss.</summary>
    public IReadOnlyList<string> WrittenPaths() =>
        [.. Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories)
            .Select(p => Path.GetRelativePath(Root, p).Replace('\\', '/'))
            .Where(p => !p.StartsWith(".git/", StringComparison.Ordinal))
            .OrderBy(p => p, StringComparer.Ordinal)];

    private static void Write(string root, SecurityCorpusFile file)
    {
        var full = Path.Combine(root, file.Path.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, file.Content);
    }

    public ValueTask DisposeAsync()
    {
        try
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temp tree is not worth failing a measurement over.
        }
        return ValueTask.CompletedTask;
    }
}
