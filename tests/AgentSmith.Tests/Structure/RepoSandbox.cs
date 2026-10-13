using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Tests.Structure;

/// <summary>
/// 2026-10-06-8565c: a repository in memory — files keyed by repository-relative path, read and
/// written as the sandbox agent does, and 'git ls-files -z' answered from them (or from
/// <see cref="Listing"/> when a test needs a broken one).
/// </summary>
internal sealed class RepoSandbox : ISandbox
{
    public Dictionary<string, string> Files { get; } = new(StringComparer.Ordinal);
    public string? Listing { get; set; }

    /// <summary>2026-10-13-2d0c: the commit 'git merge-base origin/main HEAD' answers.</summary>
    public const string MergeBase = "merge-base-sha";

    /// <summary>The meta files as they stood at the merge-base, for 'git ls-tree' and 'git show'.</summary>
    public Dictionary<string, string> BaseFiles { get; } = new(StringComparer.Ordinal);

    /// <summary>What 'git diff -z --name-only' against the merge-base answers.</summary>
    public List<string> Changed { get; } = [];

    /// <summary>2026-10-13-2d0c: files origin/main changed after the cut — a diff against the rung,
    /// not the merge-base, would list them too.</summary>
    public List<string> MovedOnBase { get; } = [];

    /// <summary>2026-10-13-2d0c: exit code of 'git ls-tree'; non-zero is a git that could not answer.</summary>
    public int LsTreeExitCode { get; set; }

    /// <summary>What 'git ls-files -z --others' answers.</summary>
    public List<string> Untracked { get; } = [];

    /// <summary>Paths the agent refuses to read, as it does a file over 1 MB or not UTF-8.</summary>
    public HashSet<string> Unreadable { get; } = new(StringComparer.Ordinal);

    /// <summary>Exit code of the commit gate's two listings; non-zero is a git that could not answer.</summary>
    public int ChangedExitCode { get; set; }
    public int ListingExitCode { get; set; }

    /// <summary>2026-10-06-8565c: paths whose write the agent refuses, as a full disk would.</summary>
    public HashSet<string> FailingWrites { get; } = new(StringComparer.Ordinal);
    public string JobId => "repo-sandbox";

    /// <summary>Every step the sandbox was asked to run.</summary>
    public List<Step> RanSteps { get; } = [];

    public Task<StepResult> RunStepAsync(Step step, IProgress<StepEvent>? progress, CancellationToken ct)
    {
        RanSteps.Add(step);
        return Task.FromResult(step.Kind switch
        {
            StepKind.ReadFile => !Unreadable.Contains(Relative(step.Path!))
                && Files.TryGetValue(Relative(step.Path!), out var content)
                ? Result(step, 0, content) : Result(step, 1, null),
            StepKind.WriteFile => Write(step),
            StepKind.Run when step.Command == "sh" => Batch(step),
            StepKind.Run => Git(step),
            _ => Result(step, 0, string.Empty),
        });
    }

    private StepResult Git(Step step) => step.Args switch
    {
        ["symbolic-ref", ..] => Result(step, 0, "origin/main\n"),
        ["rev-parse", "--verify", "--quiet", "origin/main^{commit}"] => Result(step, 0, "origin-main-sha\n"),
        ["rev-parse", "--verify", ..] => Result(step, 1, null),
        ["merge-base", "origin/main", "HEAD"] => Result(step, 0, MergeBase + "\n"),
        ["diff", "-z", "--name-only", "--diff-filter=d", var rev, "--"] =>
            Result(step, ChangedExitCode, Nul(Changed.Concat(rev == MergeBase ? [] : MovedOnBase).Where(Files.ContainsKey))),
        ["ls-files", "-z", "--others", ..] => Result(step, ChangedExitCode, Nul(Untracked)),
        ["ls-tree", "--name-only", MergeBase, "--", var path] =>
            Result(step, LsTreeExitCode, LsTreeExitCode == 0 && BaseFiles.ContainsKey(path) ? path + "\n" : null),
        ["show", var spec] when spec.StartsWith(MergeBase + ":", StringComparison.Ordinal) =>
            BaseFiles.TryGetValue(spec[(MergeBase.Length + 1)..], out var at) ? Result(step, 0, at) : Result(step, 128, null),
        ["ls-files", "-z", "--full-name", "--", ..] =>
            Result(step, ListingExitCode, Listing ?? string.Join('\0', Matching([.. step.Args!.Skip(4)])) + "\0\n"),
        _ => Result(step, 0, string.Empty),
    };

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    // A pathspec "dir/*.ext" as git reads it: '*' crosses directories.
    private IEnumerable<string> Matching(string[] specs) => Files.Keys.Order().Where(path => specs.Any(spec =>
        spec.Split('*', 2) is [var prefix, var suffix]
        && path.StartsWith(prefix, StringComparison.Ordinal) && path.EndsWith(suffix, StringComparison.Ordinal)));

    // 2026-10-13-ee83: the batched read as the script prints it; an unreadable file gets no line.
    private StepResult Batch(Step step)
    {
        var paths = step.Args!.Skip(3).ToList();
        var lines = paths.Select((path, i) => !Unreadable.Contains(path) && Files.TryGetValue(path, out var content)
            ? $"{i} {Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(content))}\n" : string.Empty);
        return Result(step, 0, string.Concat(lines) + $"end {paths.Count}\n");
    }

    private StepResult Write(Step step)
    {
        if (FailingWrites.Contains(Relative(step.Path!))) return Result(step, 1, null);
        Files[Relative(step.Path!)] = step.Content ?? string.Empty;
        return Result(step, 0, string.Empty);
    }

    private static string Nul(IEnumerable<string> paths) => string.Concat(paths.Select(p => p + "\0")) + "\n";

    private static string Relative(string path) =>
        path.StartsWith("/work/", StringComparison.Ordinal) ? path["/work/".Length..] : path;

    private static StepResult Result(Step step, int exit, string? output) =>
        new(StepResult.CurrentSchemaVersion, step.StepId, exit, false, 0.01, null, output);
}
