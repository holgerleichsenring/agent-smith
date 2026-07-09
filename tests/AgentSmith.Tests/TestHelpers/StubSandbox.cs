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
internal sealed class StubSandbox : ISandbox
{
    public string JobId { get; } = "stub-" + Guid.NewGuid().ToString("N")[..8];
    public List<Step> RanSteps { get; } = new();

    // p0239: model git staging so a scripted WriteFile is visible to
    // `git diff --cached` — without this the fast-tier harness could never make
    // `anyCode` true, so the keystone-SUCCESS-with-real-change path was untestable.
    // Repo-relative writes (what `git add -A` in /work would stage) are tracked;
    // absolute system paths (/root/.nuget credentials) are not in the repo.
    private readonly List<string> _stagedFiles = new();

    // p0193-fix follow-up: remember written contents so a subsequent ReadFile
    // of the same path returns what was written (write-then-read fidelity).
    // BootstrapRoundHandler verifies context.yaml exists on the sandbox after
    // the round — without this the fast tier could never satisfy that check.
    private readonly Dictionary<string, string> _writtenFiles = new(StringComparer.Ordinal);

    public Task<StepResult> RunStepAsync(Step step, IProgress<StepEvent>? progress, CancellationToken cancellationToken)
    {
        RanSteps.Add(step);
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
            StepKind.Run when IsGitDiff(step) => GitDiffOutput(step),
            _ => string.Empty,
        };
        return Task.FromResult(new StepResult(
            StepResult.CurrentSchemaVersion, step.StepId, ExitCode: 0,
            TimedOut: false, DurationSeconds: 0.01, ErrorMessage: null, OutputContent: output));
    }

    private static string Normalize(string path) =>
        path.StartsWith("/work/", StringComparison.Ordinal) ? path["/work/".Length..] : path;

    private static bool IsGitDiff(Step step) =>
        step.Command == "git" && step.Args is { } a && a.Contains("diff");

    // `git diff --cached --name-only` -> the staged repo-relative names;
    // `git diff --cached` (content) -> a non-empty diff when anything is staged.
    private string GitDiffOutput(Step step)
    {
        if (step.Args is { } a && a.Contains("--name-only"))
            return string.Join("\n", _stagedFiles);
        return _stagedFiles.Count > 0 ? "diff --git a/staged b/staged\n+stub change\n" : string.Empty;
    }

    // Synthetic workspace tree for handlers that enumerate files. Non-md
    // entry covers BootstrapDocument's "find a non-md source" path; the
    // .agentsmith/ entries cover LoadContext / LoadCodingPrinciples.
    private static string DefaultListing(string? path)
    {
        if (string.IsNullOrEmpty(path)) return "[]";
        if (path.Contains(".agentsmith/contexts", StringComparison.Ordinal))
            return "[\"default\"]";
        return "[\"document.txt\"]";
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
