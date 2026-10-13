using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.Tickets;

/// <summary>
/// 2026-10-13-9a59a: how one attempt ended, as the list row and the detail mark it. Both hand-back
/// producers count: the derivation hand-back parks the run with no pending question and no spec
/// row, the premise hand-back writes a handed_back spec row and fails the run.
/// </summary>
public sealed class TicketAttemptMarker(InfrastructureEnd infrastructure)
{
    /// <summary>The status RunSpecProjection writes for a handed-back spec.</summary>
    public const string HandedBackSpec = "handed_back";

    /// <param name="launches">2026-10-13-9a59n: the runs' last launches; null where they are not read.</param>
    public TicketAttemptMark Mark(Run run, TicketLiveState live, ILookup<string, string> specStatuses,
        IReadOnlyDictionary<string, RunLastLaunch>? launches = null) =>
        new(run.Id, run.Status, infrastructure.Holds(run, launches?.GetValueOrDefault(run.Id)),
            IsHandedBack(run, live, specStatuses));

    public bool IsHandedBack(Run run, TicketLiveState live, ILookup<string, string> specStatuses) =>
        (run.Status == RunStatuses.WaitingForInput && !live.Pending.ContainsKey(run.Id))
        || specStatuses[run.Id].Contains(HandedBackSpec, StringComparer.Ordinal);
}
