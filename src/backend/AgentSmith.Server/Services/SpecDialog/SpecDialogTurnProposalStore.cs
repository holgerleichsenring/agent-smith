using AgentSmith.Contracts.Models;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-11-10-d854c: each design turn's own proposal, keyed by its run id, so a later turn's proposal never hides an
/// earlier one. 2026-11-17-a0b6d: the rows are written by <see cref="SpecDialogProposalState"/> — the turn's final
/// proposal — with their decision state; this reads them for the view.
/// </summary>
public sealed class SpecDialogTurnProposalStore(
    SpecDialogProposalRepository repository, ILogger<SpecDialogTurnProposalStore> logger)
{
    /// <summary>The conversation's turn proposals by run id with when each was written and the id it awaits under, if it
    /// does; a row that cannot be read is left out.</summary>
    public async Task<IReadOnlyDictionary<string, SpecDialogTurnProposalRead>> ReadAsync(string sessionId, CancellationToken ct)
    {
        var read = new Dictionary<string, SpecDialogTurnProposalRead>(StringComparer.Ordinal);
        foreach (var (runId, json, at, proposalId, proposalState) in await repository.ReadBySessionAsync(sessionId, ct))
        {
            try
            {
                read[runId] = new(OutcomeProposalJson.Read(json), at,
                    proposalState == SpecDialogProposalClaim.Awaiting ? proposalId : null);
            }
            catch (Exception ex) when (ex is System.Text.Json.JsonException or InvalidOperationException)
            {
                logger.LogWarning(ex, "The proposal of run {RunId} on {SessionId} cannot be read", runId, sessionId);
            }
        }
        return read;
    }
}

/// <summary>2026-11-11-205ff: one turn's stored proposal and when it was last written; 2026-11-17-a0b6d: the id it awaits a
/// decision under, null unless it is the awaiting one.</summary>
public sealed record SpecDialogTurnProposalRead(OutcomeProposal Proposal, DateTimeOffset At, string? AwaitingId = null);
