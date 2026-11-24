using AgentSmith.Application.Services.Pocs.Acts;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Services.Pocs.Import;
using AgentSmith.Server.Services.Tickets;

namespace AgentSmith.Server.Services.Pocs.Acts;

/// <summary>
/// 2026-11-23-c81ed: a lifecycle act starts a run — the route never changes the PoC itself. The request's own rules (a
/// rollback names a revision, a delete repeats the name, an apply's readiness is a '/' path or 'tcp') answer 400; the
/// act's precondition (<see cref="PocActPlan"/>, the table the run's step reads again) answers 409 naming the act and the
/// state; then the PoC's slot is claimed through the run gate (a live run answers 409 naming it) and the act's preset is
/// queued in the PoC's project with string seeds. 2026-11-23-c81ee: the lifetime pass launches through here too, with its
/// case as a seed; an expired PoC's start is refused (PocExpiry).
/// </summary>
internal sealed class PocActLauncher(PocRunGate gate, PocActPlan plan, TicketProjects projects, Lifetime.PocExpiry expiry)
{
    public async Task<(PocActAnswer? Answer, PocRefusal? Refusal)> LaunchAsync(
        Poc poc, string act, PocActRequest request, string actor, CancellationToken ct, string? handover = null,
        string? lifetimeCase = null)
    {
        ArgumentNullException.ThrowIfNull(poc);
        ArgumentNullException.ThrowIfNull(request);
        if (!PocActKinds.IsAct(act)) return (null, PocRefusal.NotFound);
        if (WhyBad(poc, act, request) is { } bad) return (null, PocRefusal.BadRequest(bad));
        if (plan.WhyNot(act, expiry.Subject(poc), lifetimeCase) is { } why) return (null, PocRefusal.Conflict(Sentence(why)));
        var seeds = Seeds(poc, act, request, actor);
        if (lifetimeCase is not null) seeds[ContextKeys.PocLifetimeCase] = lifetimeCase; // 2026-11-23-c81ee
        var (runId, heldBy) = await gate.QueueAsync(poc, PocActKinds.PresetOf(act), $"starting — {act} of {poc.Name}", seeds, ct, handover);
        return runId is null ? (null, PocImportRefusals.InProgress(poc, heldBy)) : (new PocActAnswer(poc.Id, act, runId), null);
    }

    private string? WhyBad(Poc poc, string act, PocActRequest request)
    {
        if (act == PocActKinds.Rollback && request.Revision is null) return "A rollback names the revision to roll back to.";
        if (act == PocActKinds.Delete && !string.Equals(request.ConfirmName?.Trim(), poc.Name, StringComparison.Ordinal))
            return $"A delete is confirmed by typing the PoC's name '{poc.Name}'; '{request.ConfirmName}' is not it.";
        if (request.Readiness is null) return null;
        if (act != PocActKinds.Apply) return "Only an apply sets the readiness probe.";
        var (_, _, refused) = plan.Readiness(request.Readiness, projects.Config().Pocs.ReadinessPathMaxChars);
        return refused is null ? null : Sentence(refused);
    }

    private static Dictionary<string, object> Seeds(Poc poc, string act, PocActRequest request, string actor)
    {
        var seeds = new Dictionary<string, object>
        {
            [ContextKeys.PocId] = poc.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), [ContextKeys.PocActor] = actor,
        };
        if (act == PocActKinds.Rollback) seeds[ContextKeys.PocRevision] = request.Revision!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (request.Readiness is { } readiness) seeds[ContextKeys.PocReadiness] = readiness;
        return seeds;
    }

    private static string Sentence(string why) => char.ToUpperInvariant(why[0]) + why[1..] + (why.EndsWith('.') ? string.Empty : ".");
}
