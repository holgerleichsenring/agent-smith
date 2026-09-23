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
internal sealed class StubSandbox : IHoldableSandbox
{
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
            StepKind.Run when IsGitDiff(step) => GitDiffOutput(step),
            _ => string.Empty,
        };
        return Task.FromResult(new StepResult(
            StepResult.CurrentSchemaVersion, step.StepId, ExitCode: 0,
            TimedOut: false, DurationSeconds: 0.01, ErrorMessage: null, OutputContent: output));
    }

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
