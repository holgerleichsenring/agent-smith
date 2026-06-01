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

    public Task<StepResult> RunStepAsync(Step step, IProgress<StepEvent>? progress, CancellationToken cancellationToken)
    {
        RanSteps.Add(step);
        if (step.Kind == StepKind.Run && progress is not null)
        {
            progress.Report(new StepEvent(
                StepEvent.CurrentSchemaVersion, step.StepId,
                StepEventKind.Stdout, "stub stdout", DateTimeOffset.UtcNow));
        }
        var output = step.Kind switch
        {
            StepKind.ListFiles => DefaultListing(step.Path),
            StepKind.ReadFile => string.Empty,
            StepKind.WriteFile => $"File written: {step.Path}",
            _ => string.Empty,
        };
        return Task.FromResult(new StepResult(
            StepResult.CurrentSchemaVersion, step.StepId, ExitCode: 0,
            TimedOut: false, DurationSeconds: 0.01, ErrorMessage: null, OutputContent: output));
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
