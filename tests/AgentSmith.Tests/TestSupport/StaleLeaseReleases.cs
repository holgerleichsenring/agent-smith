using AgentSmith.Application.Services.Lifecycle;
using AgentSmith.Application.Services.Persistence;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.TestSupport;

/// <summary>
/// 2026-09-25-b4d9: the reaper's per-candidate collaborator, for the tests that exercise the
/// SCAN. The in-memory taken-ticket store is the default because a test bed that never claimed
/// anything holds no record, and a candidate without a record is nobody's to hold.
/// </summary>
internal static class StaleLeaseReleases
{
    public static StaleLeaseRelease For(
        IActiveRunLease lease,
        IRunCancellationRegistry registry,
        IEventPublisher events,
        TimeProvider clock,
        ITakenTicketStore? takenTickets = null) =>
        new(lease, registry, events, takenTickets ?? new InMemoryTakenTicketStore(), clock,
            NullLogger<StaleLeaseRelease>.Instance);
}
