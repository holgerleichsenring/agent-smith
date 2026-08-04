using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0393a: splices one master → verify → record block into the pipeline per
/// derived phase, in order.
/// <para>
/// The sequence is where TERMINATION comes from: each phase ends at its own done-list
/// and its own VerifyPhase, so stopping is structural instead of a judgement the model
/// has to reach. A red VerifyPhase fails its step, which stops the pipeline at that
/// phase and leaves the remaining blocks unexecuted — the executor's finalizer tail
/// still runs, so the half-migrated state reaches the pull request instead of
/// disappearing with the run.
/// </para>
/// </summary>
public sealed class PhaseSequenceHandler(ILogger<PhaseSequenceHandler> logger)
    : ICommandHandler<PhaseSequenceContext>
{
    // The block lives on PipelinePresets, not here: the park-capability questions are
    // asked against the preset at CONFIGURATION time, before any run splices anything.
    private static IReadOnlyList<string> PerPhase => PipelinePresets.CodePhaseBlock;

    public Task<CommandResult> ExecuteAsync(
        PhaseSequenceContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!context.Pipeline.TryGet<SpecSet>(ContextKeys.SpecSet, out var set)
            || set is null || set.Phases.Count == 0)
            return Task.FromResult(CommandResult.Ok(
                "No derived phases to execute — the run has no spec set"));

        var pending = set.UnexecutedTail;
        if (pending.Count == 0)
            return Task.FromResult(CommandResult.Ok(
                $"Every phase of {set.Key} already executed on this branch — nothing left to run"));

        context.Pipeline.Set(ContextKeys.SpecSequenceProgress, SpecSequenceProgress.ForSet(set));
        var commands = pending
            .SelectMany(phase => PerPhase.Select(
                name => new PipelineCommand(name) { PhaseId = phase.PhaseId }))
            .ToArray();

        logger.LogInformation(
            "Spec set {Key}: {Pending} phase(s) to execute — {Steps} spliced steps ({Phases})",
            set.Key, pending.Count, commands.Length,
            string.Join(", ", pending.Select(p => p.PhaseId)));

        return Task.FromResult(CommandResult.OkAndContinueWith(
            $"{pending.Count} phase(s) to work through: "
            + string.Join(", ", pending.Select(p => $"{p.PhaseId} ({p.Draft.Goal})")),
            commands));
    }
}
