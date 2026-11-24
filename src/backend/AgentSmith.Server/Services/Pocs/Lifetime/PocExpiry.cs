using AgentSmith.Application.Services.Pocs.Lifetime;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Services.Pocs.Acts;
using AgentSmith.Server.Services.Tickets;

namespace AgentSmith.Server.Services.Pocs.Lifetime;

/// <summary>2026-11-23-c81ee: a Poc row as PocActPlan's table reads it, with when it expired while it is expired — so the
/// launcher and the detail page refuse a start of an expired PoC the way the run's step does.</summary>
internal sealed class PocExpiry(PocLifetime lifetime, TicketProjects projects, TimeProvider clock)
{
    public PocActSubject Subject(Poc poc)
    {
        var subject = PocActSubjects.Map(poc);
        return subject with
        {
            ExpiredOn = lifetime.ExpiredOn(subject.Lifetime, lifetime.TargetOf(projects.Config().Pocs, poc.Target), clock.GetUtcNow()),
        };
    }
}
