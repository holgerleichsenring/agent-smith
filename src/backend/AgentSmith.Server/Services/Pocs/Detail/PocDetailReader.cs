using System.Security.Claims;
using AgentSmith.Application.Services.Pocs.Acts;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Security;
using AgentSmith.Server.Services.Pocs.Acts;
using AgentSmith.Server.Services.Tickets;

namespace AgentSmith.Server.Services.Pocs.Detail;

/// <summary>
/// 2026-11-23-c81ed: a PoC's detail page and its revisions. The routes already required runs.read in the PoC's project;
/// which acts are offered is <see cref="PocActPlan"/>'s table for the row, and none for a caller who may not act on it
/// (<see cref="PocActAccess"/>). A revision may be rolled back to when the plan would admit that rollback.
/// </summary>
internal sealed class PocDetailReader(
    PocRepository pocs, PocAuditRepository audit, PocActAccess access, PocActPlan plan, TicketProjects projects,
    Lifetime.PocExpiry expiry, Lifetime.PocLifetimeViews lifetimes)
{
    public async Task<PocDetailAnswer?> ReadAsync(ClaimsPrincipal user, long pocId, CancellationToken ct)
    {
        if (await pocs.FindAsync(pocId, ct) is not { } poc) return null;
        var config = projects.Config().Pocs;
        var pattern = config.Targets.FirstOrDefault(t => string.Equals(t.Name, poc.Target, StringComparison.OrdinalIgnoreCase))?.DomainPattern;
        var host = string.IsNullOrWhiteSpace(pattern) ? null : pattern.Replace("{app}", poc.Name, StringComparison.Ordinal);
        var subject = expiry.Subject(poc);
        var mayAct = access.MayAct(user, poc);
        var entries = await audit.ListAsync(poc.Id, ct);
        var expired = entries.Where(e => e.Act == PocActs.Expired).Select(e => (long?)e.AtTicks).Max(); // 2026-11-23-c81ee
        return new PocDetailAnswer(poc.Id, poc.Name, poc.Project, poc.Target, host,
            poc.State == PocStates.Ready && host is not null ? $"https://{host}" : null, poc.State, poc.OwnerName, poc.LastRunId,
            new PocDesiredView(poc.Replicas, poc.Revision, poc.ImageDigest, poc.ReadinessPath), subject.IsDeletePending, mayAct,
            Acts(subject, mayAct), config.ReadinessPathMaxChars,
            [.. entries.Select(e => new PocAuditView(new DateTimeOffset(e.AtTicks, TimeSpan.Zero), e.Actor, e.Act, e.Reason, e.RunId))])
        {
            Lifetime = lifetimes.Of(user, poc, config, expired),
        };
    }

    public async Task<PocRevisionsAnswer?> RevisionsAsync(ClaimsPrincipal user, long pocId, CancellationToken ct)
    {
        if (await pocs.FindAsync(pocId, ct) is not { } poc) return null;
        var subject = expiry.Subject(poc);
        var mayAct = access.MayAct(user, poc);
        var max = projects.Config().Pocs.ReadinessPathMaxChars;
        return new PocRevisionsAnswer([.. (await pocs.RevisionsAsync(poc.Id, ct)).Select(r => new PocRevisionRow(
            r.Number, r.State, r.Digest, r.RolloutState, r.RolloutAtTicks is { } at ? new DateTimeOffset(at, TimeSpan.Zero) : null,
            r.RunId, r.CommitSha, mayAct && MayRollBack(subject, r, max), r.Number == poc.Revision))]);
    }

    private bool MayRollBack(PocActSubject poc, PocRevision revision, int max) =>
        plan.Decide(new PocActOrder(PocActKinds.Rollback, revision.Number), poc,
            new PocActRevision(revision.Number, revision.Digest, revision.CommitSha, revision.RolloutState), max).Refusal is null;

    private PocActsView Acts(PocActSubject poc, bool mayAct) => new(
        mayAct && plan.WhyNot(PocActKinds.Stop, poc) is null, mayAct && plan.WhyNot(PocActKinds.Start, poc) is null,
        mayAct && plan.WhyNot(PocActKinds.Rollback, poc) is null, mayAct && plan.WhyNot(PocActKinds.Apply, poc) is null,
        mayAct && plan.WhyNot(PocActKinds.Delete, poc) is null);
}
