using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Structure;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.SpecExecution;

/// <summary>
/// p0437: a spec's work reaches the branch BEFORE the gate that judges it.
/// <para>
/// Measured live on ticket 19106: the master wrote its inventory, the delivery diff was
/// taken, the gate reported "this branch carries no source change", the accountant called
/// five satisfied criteria outstanding — and the work reached the branch seven seconds
/// later, in the run-level CommitAndPR that runs once after ALL specs. The verdict was a
/// false negative, and it was not a race: the gate simply stood before the delivery, every
/// run, every spec.
/// </para>
/// <para>
/// p0360's checkpointer would have carried the work, but it fires when the progress ledger
/// flips and at most once per interval. Whether a gate can see what it judges must not
/// depend on an unrelated timer, so this asks the same mechanism directly — same secret
/// scan, same staging rules, one place that knows how to put work on a branch.
/// </para>
/// <para>
/// And this is where the framework holds the work to its Limits — after the
/// push, so nothing is lost, and before VerifySpec, so a correction is still part of the spec.
/// </para>
/// <para>
/// 2026-10-12-fbbdb: and before the push, each context's own formatter rewrites the files the
/// spec changed, so the branch carries formatted code and the gate measures it. A formatter
/// that fails is said in the step result, never a failure of the step.
/// </para>
/// </summary>
public sealed class CommitSpecWorkHandler(
    SpecFormatRun formatRun,
    RunWorkCheckpointer checkpointer,
    StructureGate structureGate,
    StructureGateOutcome structureOutcome,
    ILogger<CommitSpecWorkHandler> logger)
    : ICommandHandler<CommitSpecWorkContext>
{
    public async Task<CommandResult> ExecuteAsync(
        CommitSpecWorkContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var formatNote = await formatRun.RunAsync(context.Pipeline, cancellationToken);
        await checkpointer.PushNowAsync(context.Pipeline, cancellationToken);
        logger.LogInformation("Spec work committed — the gate now reads a branch that carries it.");
        var verdict = await structureGate.JudgeAsync(context.Pipeline, cancellationToken);
        return await structureOutcome.ResolveAsync(context.Pipeline, verdict, cancellationToken, formatNote);
    }
}
