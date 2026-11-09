using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// 2026-11-09-9631a: serves one scope's steps. It opens the slot's sandbox and runs through the
/// <see cref="SandboxStepWatch"/>; when the sandbox turns out to be gone it reopens and repeats the
/// step once. Failing to open and failing to read are told apart, in the log as well.
/// </summary>
internal sealed class ReopeningStepRunner(string name, SandboxStepWatch? watch, ILogger logger) : IDisposable
{
    public ReopeningSandboxSlot Slot { get; } = new(name, logger);

    /// <summary>
    /// Runs a step that has passed its scope's guard. <paramref name="notRepeated"/> names, for a
    /// step that must not be repeated on a fresh sandbox, what the caller is told instead.
    /// </summary>
    public async Task<StepResult> RunAsync(
        Step step, IProgress<StepEvent>? progress, Func<CancellationToken, Task<ISandbox>> open,
        Func<Exception, string> openFailure, Func<Step, string?> notRepeated, CancellationToken ct)
    {
        if (Slot.Unavailable is { } why) return SourceScopeRefusal.Because(step, why);
        ISandbox sandbox;
        try
        {
            sandbox = await Slot.EnsureAsync(open, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Scope '{Name}' failed to materialise", name);
            return SourceScopeRefusal.Because(step, openFailure(ex));
        }
        try
        {
            return await RunThroughAsync(sandbox, step, progress, ct);
        }
        catch (SandboxVanishedException gone)
        {
            logger.LogInformation("Scope '{Name}': {Gone} Opening a fresh sandbox", name, gone.Message);
            return await RecoverAsync(sandbox, step, progress, open, notRepeated, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "A read through scope '{Name}' failed", name);
            return SourceScopeRefusal.Because(step, ex.Message);
        }
    }

    private Task<StepResult> RunThroughAsync(
        ISandbox sandbox, Step step, IProgress<StepEvent>? progress, CancellationToken ct) =>
        watch is null
            ? sandbox.RunStepAsync(step, progress, ct)
            : watch.RunAsync(sandbox, step, progress, ct);

    private async Task<StepResult> RecoverAsync(
        ISandbox dead, Step step, IProgress<StepEvent>? progress, Func<CancellationToken, Task<ISandbox>> open,
        Func<Step, string?> notRepeated, CancellationToken ct)
    {
        try
        {
            var fresh = await Slot.ReopenAsync(dead, open, ct);
            return notRepeated(step) is { } instead
                ? SourceScopeRefusal.Because(step, instead)
                : await RunThroughAsync(fresh, step, progress, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Scope '{Name}' could not be reopened", name);
            return SourceScopeRefusal.Because(step, Slot.MarkUnavailable(ex));
        }
    }

    public void Dispose() => Slot.Dispose();
}
