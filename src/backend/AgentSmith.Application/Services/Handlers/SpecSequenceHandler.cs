using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Pipeline;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0393a: splices one master → verify → record block into the pipeline per
/// derived spec, in order.
/// <para>
/// The sequence is where TERMINATION comes from: each spec ends at its own done-list
/// and its own VerifySpec, so stopping is structural instead of a judgement the model
/// has to reach. A red VerifySpec fails its step, which stops the pipeline at that
/// spec and leaves the remaining blocks unexecuted — the executor's finalizer tail
/// still runs, so the half-migrated state reaches the pull request instead of
/// disappearing with the run.
/// </para>
/// </summary>
public sealed class SpecSequenceHandler(ILogger<SpecSequenceHandler> logger)
    : ICommandHandler<SpecSequenceContext>
{
    // The block lives on PipelinePresets, not here: the park-capability questions are
    // asked against the preset at CONFIGURATION time, before any run splices anything.
    private static IReadOnlyList<string> PerSpec => PipelinePresets.CodeSpecBlock;

    public Task<CommandResult> ExecuteAsync(
        SpecSequenceContext context, CancellationToken cancellationToken)
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
        // 2026-09-17-0e79e: the executor's loop guard is sized here, because this is where
        // the work is known. The UNEXECUTED tail is the right number: on a re-trigger after
        // four executed specs the set's own count is twice what this run will splice.
        context.Pipeline.Set(ContextKeys.StepBudget, StepBudget.ForSpecs(pending.Count));
        var commands = pending
            .SelectMany(spec => PerSpec.Select(
                name => new PipelineCommand(name) { SpecId = spec.PhaseId }))
            .ToArray();

        logger.LogInformation(
            "Spec set {Key}: {Pending} spec(s) to execute — {Steps} spliced steps ({Specs})",
            set.Key, pending.Count, commands.Length,
            string.Join(", ", pending.Select(p => p.PhaseId)));

        return Task.FromResult(CommandResult.OkAndContinueWith(
            $"{pending.Count} phase(s) to work through: "
            + string.Join(", ", pending.Select(p => $"{p.PhaseId} ({p.Draft.Goal})")),
            commands));
    }
}
