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
    IReadOnlyDictionary<string, string>? sourceFiles = null)
    : IHoldableSandbox, ISandboxToolchainImage
{
    /// <summary>2026-10-12-50bda: the image a container backend would name; null (the default)
    /// records none, as the in-process backend does.</summary>
    public string ToolchainImage => toolchainImage!;

    public string JobId { get; } = "stub-" + Guid.NewGuid().ToString("N")[..8];
    public List<Step> RanSteps { get; } = new();

    // p0239: model git staging so a scripted WriteFile is visible to `git diff --cached`.
    // Repo-relative writes (what `git add -A` in /work would stage) are tracked; absolute
    // system paths (/root/.nuget credentials) are not in the repo.
    private readonly List<string> _stagedFiles = new();

    // p0193-fix follow-up: remember written contents so a subsequent ReadFile of the same
    // path returns what was written — BootstrapRoundHandler verifies context.yaml exists.
    private readonly Dictionary<string, string> _writtenFiles = new(StringComparer.Ordinal);

    /// <summary>2026-09-22-2d11b: the remote the last clone into this work path named.</summary>
    public string? Origin { get; private set; }

    /// <summary>What rev-parse answers; a refreshed tree lands on a different one.</summary>
    public string Head { get; set; } = "stub-head";

    public Task<StepResult> RunStepAsync(Step step, IProgress<StepEvent>? progress, CancellationToken cancellationToken)
    {
        RanSteps.Add(step);
        if (IsGit(step, "clone"))
            Origin = step.Args?.FirstOrDefault(a => a.Contains("://", StringComparison.Ordinal));
        if (step.Kind == StepKind.WriteFile && step.Path is { } wp)
        {
            var rel = wp.StartsWith("/work/", StringComparison.Ordinal) ? wp["/work/".Length..] : wp;
            if (!rel.StartsWith('/') && !_stagedFiles.Contains(rel)) _stagedFiles.Add(rel);
            _writtenFiles[Normalize(wp)] = step.Content ?? string.Empty;
        }
        if (step.Kind == StepKind.Run && progress is not null)
        {
            progress.Report(new StepEvent(
                StepEvent.CurrentSchemaVersion, step.StepId,
                StepEventKind.Stdout, "stub stdout", DateTimeOffset.UtcNow));
        }
        // 2026-10-02-3f06c: a path the test declared missing answers as the real reader does.
        if (step.Kind == StepKind.ReadFile && step.Path is { } missing
            && missingPaths?.Contains(Normalize(missing)) == true)
            return Task.FromResult(new StepResult(
                StepResult.CurrentSchemaVersion, step.StepId, ExitCode: 1, TimedOut: false,
                DurationSeconds: 0.01, ErrorMessage: StepErrors.FileNotFound(missing), OutputContent: null));
        // A base the branch was cut from, opted into by a test — the commit gate lists what the
        // branch changed against it and reads its Limits and baseline there. Without one the
        // gate finds no base and passes.
        if (baseFiles is not null && BaseAnswer(step) is { } answer) return Task.FromResult(answer);
        // 2026-10-06-8565c: tracked sources a test seeded — listed by 'git ls-files -z --full-name'
        // and read back. Without any, the listing is empty and the init takes no baseline.
        if (sourceFiles is not null && SourceAnswer(step) is { } source) return Task.FromResult(source);
        var output = step.Kind switch
        {
            StepKind.ListFiles => DefaultListing(step.Path),
            StepKind.ReadFile => step.Path is { } rp
                ? _writtenFiles.GetValueOrDefault(Normalize(rp), string.Empty)
                : string.Empty,
            StepKind.WriteFile => $"File written: {step.Path}",
            // p0439: a verified head to name, and nothing beyond it. 2026-09-22-2d11b: and
            // who the work path is a clone OF, which is what tells the refresh rung apart.
            StepKind.Run when IsGit(step, "remote.origin.url") => Origin ?? string.Empty,
            StepKind.Run when IsGit(step, "rev-parse") => Head,
            StepKind.Run when IsGit(step, "--name-status") => string.Empty,
            StepKind.Run when reportsWorkingTree && IsGit(step, "--porcelain") =>
                string.Concat(_stagedFiles.Select(file => $"?? {file}\n")),
            StepKind.Run when IsGitDiff(step) => GitDiffOutput(step),
            _ => string.Empty,
        };
        return Task.FromResult(new StepResult(
            StepResult.CurrentSchemaVersion, step.StepId, ExitCode: 0,
            TimedOut: false, DurationSeconds: 0.01, ErrorMessage: null, OutputContent: output));
    }

    private StepResult? BaseAnswer(Step step)
    {
        if (IsGit(step, "symbolic-ref")) return Answer(step, 0, "origin/main\n");
        // The gate's NUL-separated listings: every written file is changed, none untracked.
        if (IsGit(step, "-z") && IsGit(step, "--name-only"))
            return Answer(step, 0, string.Concat(_stagedFiles.Select(f => f + "\0")) + "\n");
        if (IsGit(step, "-z") && IsGit(step, "--others")) return Answer(step, 0, "\n");
        if (!IsGit(step, "show") || step.Args?.LastOrDefault() is not { } spec
            || !spec.StartsWith("origin/main:", StringComparison.Ordinal))
            return null;
        return baseFiles!.TryGetValue(spec["origin/main:".Length..], out var content)
            ? Answer(step, 0, content) : Answer(step, 128, null);
    }

    private StepResult? SourceAnswer(Step step)
    {
        if (step.Kind == StepKind.ReadFile && step.Path is { } path
            && sourceFiles!.TryGetValue(Normalize(path), out var content))
            return Answer(step, 0, content);
        if (!IsGit(step, "--full-name") || step.Args is not { } args) return null;
        // A pathspec "dir/*.ext" as git reads it: '*' crosses directories.
        var specs = args.SkipWhile(a => a != "--").Skip(1).Select(s => s.Split('*', 2)).ToList();
        var listed = sourceFiles!.Keys.Order(StringComparer.Ordinal).Where(p => specs.Any(s =>
            s.Length == 2 && p.StartsWith(s[0], StringComparison.Ordinal) && p.EndsWith(s[1], StringComparison.Ordinal)));
        return Answer(step, 0, string.Join('\0', listed) + "\0\n");
    }

    private static StepResult Answer(Step step, int exit, string? output) => new(
        StepResult.CurrentSchemaVersion, step.StepId, exit, TimedOut: false, DurationSeconds: 0.01,
        ErrorMessage: null, OutputContent: output);

    private static string Normalize(string path) =>
        path.StartsWith("/work/", StringComparison.Ordinal) ? path["/work/".Length..] : path;

    private static bool IsGitDiff(Step step) => IsGit(step, "diff");

    private static bool IsGit(Step step, string arg) =>
        step.Command == "git" && step.Args is { } a && a.Contains(arg);

    // `git diff --cached --name-only` -> the staged repo-relative names; `git diff …` -> a
    // real unified diff NAMING them. p0422: the delivery account cites the file that
    // satisfies each criterion, so an unresolvable name reports nothing delivered.
    private string GitDiffOutput(Step step)
    {
        if (step.Args is { } a && a.Contains("--name-only"))
            return string.Join("\n", _stagedFiles);
        if (_stagedFiles.Count == 0) return string.Empty;
        return string.Concat(_stagedFiles.Select(file =>
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
