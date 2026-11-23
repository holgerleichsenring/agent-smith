using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Pocs.Scan;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-11-23-933dc: the gate step of a poc-deploy run, after the intake and before anything reaches git. A revision
/// already scanned keeps its verdict — a blocked one ends the run blocked again, a passed one goes on to the commit —
/// unless the run is an overrule, which appends the lifted ids under 'code' and scans again.
/// </summary>
public sealed class PocCodeScanHandler(IPocScanStore store, PocScanGate gate) : ICommandHandler<PocCodeScanContext>
{
    public async Task<CommandResult> ExecuteAsync(PocCodeScanContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var seeds = context.Seeds;
        if (seeds.PocId is not { } pocId || seeds.Revision is not { } revision)
            return CommandResult.Fail($"A poc-deploy run needs the PoC and revision seeded under ContextKeys.{nameof(ContextKeys.PocId)} "
                + $"and ContextKeys.{nameof(ContextKeys.PocRevision)}.");
        if (await store.ReadAsync(pocId, revision, cancellationToken) is not { } subject)
            return CommandResult.Fail($"No PoC {pocId} with a revision {revision} exists.");
        if (Settled(subject, seeds.Overrule) is { } settled) return settled;
        if (seeds.Overrule)
        {
            await store.AppendOverruledAsync(pocId, revision, PocScanStages.Code, seeds.OverruleIds, cancellationToken);
            subject = await store.ReadAsync(pocId, revision, cancellationToken) ?? subject;
        }
        return await gate.RunAsync(context, subject, cancellationToken);
    }

    // What an earlier scan or the intake already decided, or null when this run scans.
    private static CommandResult? Settled(PocScanSubject subject, bool overrule) => subject.RevisionState switch
    {
        PocRevisionStates.Overruled => CommandResult.Ok($"Revision {subject.Revision} was overruled earlier; it is not scanned again."),
        PocRevisionStates.Blocked when !overrule => CommandResult.Fail(
            $"Revision {subject.Revision} of '{subject.PocName}' was blocked by the code scan earlier; it is not scanned again."),
        PocRevisionStates.Validated when overrule => CommandResult.Fail($"Revision {subject.Revision} is not blocked; there is nothing to overrule."),
        PocRevisionStates.Validated when subject.VerdictJson is not null => CommandResult.Ok($"Revision {subject.Revision} passed the code scan earlier."),
        PocRevisionStates.Validated or PocRevisionStates.Blocked => null,
        _ => CommandResult.Fail($"Revision {subject.Revision} of '{subject.PocName}' is {subject.RevisionState}; only a validated revision is scanned."),
    };
}
