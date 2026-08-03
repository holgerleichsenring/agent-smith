using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.PhaseExecution;

/// <summary>
/// p0315d, p0393a: the entry gate of the code pipeline. It no longer extracts anything
/// itself — DeriveSpec resolves the set under a fixed source precedence (branch
/// artifact, then a spec embedded in the ticket description, then derivation) and this
/// step is the assertion that a validated set exists before a single master token is
/// spent, plus the publication of the first phase as the current one.
/// <para>
/// A run without a set is a composition failure, not a ticket shape: every ticket now
/// gets one, and the fail-safe path still produces one phase carrying the whole ticket.
/// Failing here is what keeps that promise honest.
/// </para>
/// </summary>
public sealed class PhaseSpecGateHandler(ILogger<PhaseSpecGateHandler> logger)
    : ICommandHandler<PhaseSpecGateContext>
{
    public Task<CommandResult> ExecuteAsync(
        PhaseSpecGateContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!context.Pipeline.TryGet<SpecSet>(ContextKeys.SpecSet, out var set) || set is null)
            return Task.FromResult(CommandResult.Fail(
                $"No phase spec for ticket {context.Ticket.Id.Value}: DeriveSpec produced "
                + "neither a branch artifact, an embedded spec nor a derived set."));

        if (set.IsHandedBack)
            return Task.FromResult(CommandResult.Ok(
                $"The derivation handed ticket {context.Ticket.Id.Value} back "
                + $"({set.Handback!.Case}) — nothing to gate"));

        if (set.Phases.Count == 0)
            return Task.FromResult(CommandResult.Fail(
                $"Spec set {set.Key} carries no phase — there is nothing to build."));

        // The first phase is current until the sequence selects the next one. Publishing it
        // here keeps every downstream reader (planner, master, keystone, phase record)
        // working off one key whether or not a sequence is involved.
        var first = set.Phases[0];
        context.Pipeline.Set(ContextKeys.PhaseSpec, first.Draft);
        logger.LogInformation(
            "Spec set {Key} validated: {Count} phase(s), first is {PhaseId} ({Goal})",
            set.Key, set.Phases.Count, first.PhaseId, first.Draft.Goal);
        return Task.FromResult(CommandResult.Ok(
            set.Phases.Count == 1
                ? $"Phase spec {first.PhaseId} validated: {first.Draft.Goal}"
                : $"{set.Phases.Count} phase specs validated, starting at {first.PhaseId}: "
                  + first.Draft.Goal));
    }
}
