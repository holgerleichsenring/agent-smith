using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Tests.TestHelpers;

/// <summary>
/// p0196: in-memory ISandbox returning canned success for every step.
/// Records writes + run-commands so tests can assert what was emitted.
/// ListFiles on /work returns a synthetic source file so handlers that
/// scan the workspace (e.g. BootstrapDocument) don't see an empty tree.
/// Run-commands fire one stdout line via progress so handlers that
/// capture stdout (e.g. MarkItDown wrapper) get non-empty content.
/// </summary>
internal sealed class StubSandbox(
    IReadOnlySet<string>? missingPaths = null, string? toolchainImage = null, bool reportsWorkingTree = false,
    IReadOnlyDictionary<string, string>? baseFiles = null,
    IReadOnlyDictionary<string, string>? sourceFiles = null,
    IReadOnlyDictionary<string, (int ExitCode, string Output)>? shellAnswers = null,
    bool advancesHead = false,
    IReadOnlyDictionary<string, (int ExitCode, string Output)>? gitAnswers = null,
    string? root = "/work",
    IReadOnlyDictionary<string, string>? listedFiles = null)
    : IHoldableSandbox, ISandboxToolchainImage
{
    // 2026-11-25-9992c: the repository's root the paths arrive under — /work/<repo> behind a RepoSandbox.
    // 2026-11-25-9992b: null for a workspace container, which serves every repository of a run: each step's tree is
    // the /work/<repo> its path or working directory names (StubTree.RootOf), with state of its own.
    private readonly Dictionary<string, StubTree> _trees = new(StringComparer.Ordinal);
    private StubTree? _lastTree;
    private string _head = "stub-head";

    /// <summary>2026-10-12-50bda: the image a container backend would name; null (the default)
    /// records none, as the in-process backend does.</summary>
    public string ToolchainImage => toolchainImage!;

    public string JobId { get; } = "stub-" + Guid.NewGuid().ToString("N")[..8];
    public List<Step> RanSteps { get; } = new();

    /// <summary>2026-09-22-2d11b: the remote the last clone into this work path named.</summary>
    public string? Origin { get; private set; }

    /// <summary>What rev-parse answers in the tree a step last touched; setting it moves every tree's head.</summary>
    public string Head
    {
        get => _lastTree?.Head ?? _head;
        set
        {
            _head = value;
            foreach (var tree in _trees.Values) tree.Head = value;
        }
    }

    /// <summary>2026-11-25-9992b: the tree at <paramref name="treeRoot"/>, created on first use.</summary>
    public StubTree Tree(string treeRoot)
    {
        if (!_trees.TryGetValue(treeRoot, out var tree)) _trees[treeRoot] = tree = new StubTree(treeRoot, _head);
        return tree;
    }

    private StubTree TreeFor(Step step) => _lastTree = Tree(root ?? StubTree.RootOf(step));

    /// <summary>2026-11-09-9631a: removed under the reader: a step pushed now never gets an answer.</summary>
    public bool Vanished { get; set; }

    /// <summary>2026-11-09-9631a: told of each step that reached this sandbox, for a test that acts mid-turn.</summary>
    public Action<Step>? OnStep { get; set; }

    public async Task<StepResult> RunStepAsync(Step step, IProgress<StepEvent>? progress, CancellationToken cancellationToken)
    {
        if (Vanished) await Task.Delay(Timeout.Infinite, cancellationToken);
        OnStep?.Invoke(step);
        return await AnswerAsync(step, progress, cancellationToken);
    }

    private Task<StepResult> AnswerAsync(Step step, IProgress<StepEvent>? progress, CancellationToken cancellationToken)
    {
        RanSteps.Add(step);
        var tree = TreeFor(step);
        if (IsGit(step, "clone"))
            Origin = tree.Origin = step.Args?.FirstOrDefault(a => a?.Contains("://", StringComparison.Ordinal) == true);
        if (step.Kind == StepKind.WriteFile && step.Path is { } wp)
        {
            var rel = tree.Normalize(wp);
            if (!rel.StartsWith('/') && !tree.Staged.Contains(rel)) tree.Staged.Add(rel);
            tree.Written[rel] = step.Content ?? string.Empty;
        }
        if (step.Kind == StepKind.Run && progress is not null)
        {
            progress.Report(new StepEvent(
                StepEvent.CurrentSchemaVersion, step.StepId,
                StepEventKind.Stdout, "stub stdout", DateTimeOffset.UtcNow));
        }
        // 2026-10-02-3f06c: a path the test declared missing answers as the real reader does.
        if (step.Kind == StepKind.ReadFile && step.Path is { } missing
            && missingPaths?.Contains(tree.Normalize(missing)) == true)
            return Task.FromResult(new StepResult(
                StepResult.CurrentSchemaVersion, step.StepId, ExitCode: 1, TimedOut: false,
                DurationSeconds: 0.01, ErrorMessage: StepErrors.FileNotFound(missing), OutputContent: null));
        // A base the branch was cut from, opted into by a test — the commit gate lists what the
        // branch changed against it and reads its Limits and baseline there. Without one the
        // gate finds no base and passes.
        // 2026-10-13-a347a: a shell command a test declared failing answers as it would.
        if (step.Command == "/bin/sh" && step.Args is [_, var line] && shellAnswers?.FirstOrDefault(
                a => line.StartsWith(a.Key, StringComparison.Ordinal)) is { Key: not null } shell)
            return Task.FromResult(Answer(step, shell.Value.ExitCode, shell.Value.Output));
        // 2026-11-04-6768a: a git command whose space-joined argv starts with a key answers as scripted;
        // '{grep}' in the output stands for the command's own --grep value.
        if (step.Command == "git" && GitAnswer(step) is { } git) return Task.FromResult(git);
        if (listedFiles is not null && ListedAnswer(step, tree) is { } listed) return Task.FromResult(listed);
        if (baseFiles is not null && BaseAnswer(step, tree) is { } answer) return Task.FromResult(answer);
        // 2026-11-02-2b26d: and every write is a staged change, so each revision is a commit of its own.
        if (advancesHead && IsGit(step, "--cached") && IsGit(step, "--quiet")) return Task.FromResult(Answer(step, 1, null));
        // 2026-10-06-8565c: tracked sources a test seeded — listed by 'git ls-files -z --full-name'
        // and read back. Without any, the listing is empty and the init takes no baseline.
        if (sourceFiles is not null && SourceAnswer(step, tree) is { } source) return Task.FromResult(source);
        var output = step.Kind switch
        {
            StepKind.ListFiles => DefaultListing(step.Path),
            StepKind.ReadFile => step.Path is { } rp
                ? tree.Written.GetValueOrDefault(tree.Normalize(rp), string.Empty)
                : string.Empty,
            StepKind.WriteFile => $"File written: {step.Path}",
            // p0439: a verified head to name, and nothing beyond it. 2026-09-22-2d11b: and
            // who the work path is a clone OF, which is what tells the refresh rung apart.
            StepKind.Run when IsGit(step, "remote.origin.url") => tree.Origin ?? string.Empty,
            StepKind.Run when IsGit(step, "rev-parse") => tree.Head,
            // 2026-11-02-2b26d: opted into, a commit lands on a new head, as a second revision's write does.
            StepKind.Run when advancesHead && IsGit(step, "commit") => Advance(tree),
            StepKind.Run when IsGit(step, "--name-status") => string.Empty,
            StepKind.Run when reportsWorkingTree && IsGit(step, "--porcelain") =>
                string.Concat(tree.Staged.Select(file => $"?? {file}\n")),
            StepKind.Run when IsGitDiff(step) => GitDiffOutput(step, tree),
            _ => string.Empty,
        };
        return Task.FromResult(new StepResult(
            StepResult.CurrentSchemaVersion, step.StepId, ExitCode: 0,
            TimedOut: false, DurationSeconds: 0.01, ErrorMessage: null, OutputContent: output));
    }

    private StepResult? BaseAnswer(Step step, StubTree tree)
    {
        if (IsGit(step, "symbolic-ref")) return Answer(step, 0, "origin/main\n");
        // The gate's NUL-separated listings: every written file is changed, none untracked.
        if (IsGit(step, "-z") && IsGit(step, "--name-only"))
            return Answer(step, 0, string.Concat(tree.Staged.Select(f => f + "\0")) + "\n");
        if (IsGit(step, "-z") && IsGit(step, "--others")) return Answer(step, 0, "\n");
        // 2026-10-13-2d0c: the gate reads at the merge-base of origin/main and HEAD.
        if (IsGit(step, "merge-base")) return Answer(step, 0, BaseCommit + "\n");
        if (IsGit(step, "ls-tree") && step.Args?.LastOrDefault() is { } listed)
            return Answer(step, 0, baseFiles!.ContainsKey(listed) ? listed + "\n" : string.Empty);
        if (!IsGit(step, "show") || step.Args?.LastOrDefault() is not { } spec
            || !spec.StartsWith(BaseCommit + ":", StringComparison.Ordinal))
            return null;
        return baseFiles!.TryGetValue(spec[(BaseCommit.Length + 1)..], out var content)
            ? Answer(step, 0, content) : Answer(step, 128, null);
    }

    private const string BaseCommit = "base-commit-sha";

    private StepResult? GitAnswer(Step step)
    {
        var argv = string.Join(' ', step.Args ?? []);
        var hit = gitAnswers?.FirstOrDefault(a => argv.StartsWith(a.Key, StringComparison.Ordinal));
        if (hit is not { Key: not null } scripted) return null;
        var grep = step.Args?.FirstOrDefault(a => a.StartsWith("--grep=", StringComparison.Ordinal))?["--grep=".Length..];
        return Answer(step, scripted.Value.ExitCode, scripted.Value.Output.Replace("{grep}", grep ?? string.Empty));
    }

    private StepResult? SourceAnswer(Step step, StubTree tree)
    {
        // 2026-10-13-50c7: a file the run wrote reads back as written, not as it was seeded.
        if (step.Kind == StepKind.ReadFile && step.Path is { } path && !tree.Written.ContainsKey(tree.Normalize(path))
            && sourceFiles!.TryGetValue(tree.Normalize(path), out var content))
            return Answer(step, 0, content);
        if (!IsGit(step, "--full-name") || step.Args is not { } args) return null;
        // A pathspec "dir/*.ext" as git reads it: '*' crosses directories.
        var specs = args.SkipWhile(a => a != "--").Skip(1).Select(s => s.Split('*', 2)).ToList();
        var listed = sourceFiles!.Keys.Order(StringComparer.Ordinal).Where(p => specs.Any(s =>
            s.Length == 2 && p.StartsWith(s[0], StringComparison.Ordinal) && p.EndsWith(s[1], StringComparison.Ordinal)));
        return Answer(step, 0, string.Join('\0', listed) + "\0\n");
    }

    // 2026-11-28-d1ffa: files a test lays in every repository's tree, by repo-relative path — listed under the path a ListFiles
    // names (beside the synthetic entry) and read back by a ReadFile whose path ends in one, until the run writes it.
    private StepResult? ListedAnswer(Step step, StubTree tree)
    {
        if (step.Kind == StepKind.ListFiles && step.Path is { } dir && !dir.Contains(".agentsmith/contexts", StringComparison.Ordinal))
            return Answer(step, 0, System.Text.Json.JsonSerializer.Serialize(
                listedFiles!.Keys.Order(StringComparer.Ordinal).Select(rel => $"{dir.TrimEnd('/')}/{rel}").Append("document.txt")));
        if (step.Kind == StepKind.ReadFile && step.Path is { } path && !tree.Written.ContainsKey(tree.Normalize(path))
            && listedFiles!.FirstOrDefault(f => path.EndsWith("/" + f.Key, StringComparison.Ordinal)) is { Key: not null } file)
            return Answer(step, 0, file.Value);
        return null;
    }

    private static StepResult Answer(Step step, int exit, string? output) => new(
        StepResult.CurrentSchemaVersion, step.StepId, exit, TimedOut: false, DurationSeconds: 0.01,
        ErrorMessage: null, OutputContent: output);

    private static bool IsGitDiff(Step step) => IsGit(step, "diff");

    private static string Advance(StubTree tree)
    {
        tree.Head = $"stub-head-{++tree.Commits}";
        return string.Empty;
    }

    private static bool IsGit(Step step, string arg) =>
        step.Command == "git" && step.Args is { } a && a.Contains(arg);

    // `git diff --cached --name-only` -> the staged repo-relative names; `git diff …` -> a
    // real unified diff NAMING them. p0422: the delivery account cites the file that
    // satisfies each criterion, so an unresolvable name reports nothing delivered.
    private static string GitDiffOutput(Step step, StubTree tree)
    {
        if (step.Args is { } a && a.Contains("--name-only"))
            return string.Join("\n", tree.Staged);
        if (tree.Staged.Count == 0) return string.Empty;
        return string.Concat(tree.Staged.Select(file =>
            $"diff --git a/{file} b/{file}\n--- a/{file}\n+++ b/{file}\n@@ -1 +1 @@\n+stub change\n"));
    }

    // Synthetic workspace tree for handlers that enumerate files: the non-md entry covers
    // BootstrapDocument, the .agentsmith/ entries LoadContext / LoadCodingPrinciples.
    private static string DefaultListing(string? path)
    {
        if (string.IsNullOrEmpty(path)) return "[]";
        if (path.Contains(".agentsmith/contexts", StringComparison.Ordinal))
            return "[\"default\"]";
        return "[\"document.txt\"]";
    }

    /// <summary>2026-09-13-ed5a: whether the owner tore this sandbox down — a foreign
    /// read-only checkout left running is a container nobody owns any more.</summary>
    public bool Disposed { get; private set; }

    /// <summary>2026-09-22-2d11b: whether a hold's release took it, which never waits.</summary>
    public bool ForceRemoved { get; private set; }

    public Task ForceRemoveAsync(CancellationToken cancellationToken)
    {
        ForceRemoved = true;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        Disposed = true;
        return ValueTask.CompletedTask;
    }
}
