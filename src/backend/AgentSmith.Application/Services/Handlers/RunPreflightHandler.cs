using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Preflight;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0428: the run proves its preconditions before it spends anything on them being true.
/// <para>
/// Across 45 measured runs, at least seven died on something knowable in ten seconds
/// without a model call — an unwritable sandbox home, an empty configuration, a
/// credential that resolved to nothing — after 30 to 90 minutes had already been spent.
/// This step runs every registered <see cref="IRunPreflightCheck"/> and either passes
/// silently or fails the run naming the lever.
/// </para>
/// <para>
/// A check that CRASHES is downgraded to a warning: the crash is a bug in the check,
/// not a verdict on the run, and a preflight that fails a healthy run is worse than no
/// preflight at all.
/// </para>
/// </summary>
public sealed class RunPreflightHandler(
    IEnumerable<IRunPreflightCheck> checks,
    IEventPublisher eventPublisher,
    ILogger<RunPreflightHandler> logger) : ICommandHandler<RunPreflightContext>
{
    private const string GateName = "preflight";

    public async Task<CommandResult> ExecuteAsync(
        RunPreflightContext context, CancellationToken cancellationToken)
    {
        var findings = new List<RunPreflightFinding>();
        foreach (var check in checks)
            findings.Add(await RunGuardedAsync(check, context.Pipeline, cancellationToken));

        foreach (var warning in findings.Where(f => f.Verdict == RunPreflightVerdict.Warn))
            logger.LogWarning("Preflight — {Finding}", warning.Describe());

        var failures = findings.Where(f => f.Verdict == RunPreflightVerdict.Fail).ToList();
        var runId = context.Pipeline.TryGet<string>(ContextKeys.RunId, out var id) ? id : null;
        return failures.Count == 0
            ? await PassAsync(runId, findings, cancellationToken)
            : await FailAsync(runId, failures, cancellationToken);
    }

    private async Task<CommandResult> PassAsync(
        string? runId, IReadOnlyList<RunPreflightFinding> findings, CancellationToken ct)
    {
        var warnings = findings.Where(f => f.Verdict == RunPreflightVerdict.Warn).ToList();
        // The clean sentence is what marks this gate silent (CommandStepClasses'
        // no-op phrases); a reported finding must read differently or it hides.
        var summary = warnings.Count == 0
            ? $"Preflight: {findings.Count} precondition(s) hold."
            : "Preflight reported: " + string.Join("; ", warnings.Select(f => f.Describe()));
        await PublishGateAsync(runId, passed: true, summary, ct);
        logger.LogInformation("{Summary}", summary);
        return CommandResult.Ok(summary);
    }

    private async Task<CommandResult> FailAsync(
        string? runId, IReadOnlyList<RunPreflightFinding> failures, CancellationToken ct)
    {
        var message = "Preflight failed — the run stops here rather than discovering this an hour in:\n"
            + string.Join("\n", failures.Select(f => "  - " + f.Describe()));
        logger.LogError("{Message}", message);
        await PublishGateAsync(runId, passed: false, string.Join("; ", failures.Select(f => f.Check)), ct);
        return CommandResult.Fail(message);
    }

    private async Task<RunPreflightFinding> RunGuardedAsync(
        IRunPreflightCheck check, PipelineContext pipeline, CancellationToken ct)
    {
        try
        {
            return await check.RunAsync(pipeline, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Preflight check {Check} crashed — reported, never a verdict", check.Name);
            return RunPreflightFinding.Warn(
                check.Name, $"the check itself crashed ({ex.Message}) and proved nothing");
        }
    }

    private Task PublishGateAsync(string? runId, bool passed, string reason, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(runId)) return Task.CompletedTask;
        return eventPublisher.PublishAsync(
            new GateCheckedEvent(runId!, GateName, passed, reason, DateTimeOffset.UtcNow), ct);
    }
}
