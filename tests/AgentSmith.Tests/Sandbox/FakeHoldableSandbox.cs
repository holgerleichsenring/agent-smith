using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// 2026-09-22-2d11b: a sandbox a conversation can hold, with just enough git in it to tell
/// an empty work path from one that already carries a clone — which is the question the
/// refresh rung asks the tree itself, never the register.
/// </summary>
internal sealed class FakeHoldableSandbox : IHoldableSandbox
{
    private static int _order;

    public string JobId { get; init; } = "job-" + Guid.NewGuid().ToString("N")[..8];

    public List<Step> RanSteps { get; } = [];

    /// <summary>The remote this work path is a clone of; null until something clones into it.</summary>
    public string? Origin { get; set; }

    /// <summary>What <c>git rev-parse HEAD</c> answers — a reset onto FETCH_HEAD moves it.</summary>
    public string Head { get; set; } = "sha-before";

    /// <summary>What the tree lands on once it is refreshed. Null leaves the sha alone.</summary>
    public string? RefreshedHead { get; set; }

    /// <summary>A step whose args contain this fails, with <see cref="FailureText"/>.</summary>
    public string? FailOn { get; set; }

    public string FailureText { get; set; } = "could not resolve host: github.test";

    public int? ForceRemovedAt { get; private set; }

    public bool Disposed { get; private set; }

    public bool ThrowOnForceRemove { get; set; }

    public IReadOnlyList<string> Commands =>
        [.. RanSteps.Select(step => string.Join(" ", step.Args ?? []))];

    public bool Ran(string fragment) =>
        Commands.Any(command => command.Contains(fragment, StringComparison.Ordinal));

    public Task<StepResult> RunStepAsync(
        Step step, IProgress<StepEvent>? progress, CancellationToken cancellationToken)
    {
        RanSteps.Add(step);
        var args = string.Join(" ", step.Args ?? []);
        if (FailOn is not null && args.Contains(FailOn, StringComparison.Ordinal))
            return Task.FromResult(Result(step, exitCode: 128, FailureText));
        if (args.Contains("remote.origin.url", StringComparison.Ordinal))
            return Task.FromResult(Origin is null
                ? Result(step, exitCode: 128, "fatal: not a git repository")
                : Result(step, exitCode: 0, Origin));
        if (args.Contains("clone", StringComparison.Ordinal))
        {
            Origin = step.Args?.FirstOrDefault(a => a.Contains("://", StringComparison.Ordinal));
            return Task.FromResult(Result(step, exitCode: 0, string.Empty));
        }
        if (args.Contains("reset --hard", StringComparison.Ordinal)
            || (args.Contains("reset", StringComparison.Ordinal)
                && args.Contains("--hard", StringComparison.Ordinal)))
        {
            if (RefreshedHead is not null) Head = RefreshedHead;
            return Task.FromResult(Result(step, exitCode: 0, string.Empty));
        }
        if (args.Contains("rev-parse", StringComparison.Ordinal))
            return Task.FromResult(Result(step, exitCode: 0, Head));
        return Task.FromResult(Result(step, exitCode: 0, string.Empty));
    }

    private static StepResult Result(Step step, int exitCode, string output) =>
        new(StepResult.CurrentSchemaVersion, step.StepId, exitCode,
            TimedOut: false, DurationSeconds: 0.01,
            ErrorMessage: exitCode == 0 ? null : output,
            OutputContent: exitCode == 0 ? output : null);

    public Task ForceRemoveAsync(CancellationToken cancellationToken)
    {
        if (ThrowOnForceRemove) throw new InvalidOperationException("the daemon is not reachable");
        ForceRemovedAt = Interlocked.Increment(ref _order);
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        Disposed = true;
        return ValueTask.CompletedTask;
    }
}
