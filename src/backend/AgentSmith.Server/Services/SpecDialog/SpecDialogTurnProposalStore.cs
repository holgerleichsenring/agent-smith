using AgentSmith.Contracts.Models;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-11-10-d854c: each design turn's own proposal, keyed by its run id, beside the latest one a decision acts on.
/// Written where a proposal arises — each draft under review and the turn's final proposal — so a later turn's
/// proposal never hides an earlier one and a turn that died mid-review leaves its last draft. A write that fails is
/// logged: the awaiting proposal the person decides on is stored elsewhere and must not be lost to this one.
/// </summary>
public sealed class SpecDialogTurnProposalStore(
    SpecDialogTurnProposalRepository repository, ILogger<SpecDialogTurnProposalStore> logger)
{
    public async Task WriteAsync(ConversationState state, string? runId, OutcomeProposal proposal, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        if (string.IsNullOrEmpty(runId)) return;
        try
        {
            await repository.WriteAsync(state.JobId, runId, OutcomeProposalJson.Write(proposal), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not keep the proposal of run {RunId} on {SessionId}", runId, state.JobId);
        }
    }

    /// <summary>2026-11-13-3dc8: the turn ended without a proposal, so the draft it wrote mid-turn is not its proposal.
    /// A failure is logged, as a write's is.</summary>
    public async Task DropAsync(ConversationState state, string? runId, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(runId)) return;
        try
        {
            await repository.DeleteByRunAsync(runId, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not drop the draft of run {RunId} on {SessionId}", runId, state.JobId);
        }
    }

    /// <summary>The conversation's turn proposals by run id with when each was written; a row that cannot be read is left
    /// out. A write this store swallowed above is lost to it — making that write durable is not 2026-11-11-205ff.</summary>
    public async Task<IReadOnlyDictionary<string, SpecDialogTurnProposalRead>> ReadAsync(string sessionId, CancellationToken ct)
    {
        var read = new Dictionary<string, SpecDialogTurnProposalRead>(StringComparer.Ordinal);
        foreach (var (runId, json, at) in await repository.ReadBySessionAsync(sessionId, ct))
        {
            try
            {
                read[runId] = new(OutcomeProposalJson.Read(json), at);
            }
            catch (Exception ex) when (ex is System.Text.Json.JsonException or InvalidOperationException)
            {
                logger.LogWarning(ex, "The proposal of run {RunId} on {SessionId} cannot be read", runId, sessionId);
            }
        }
        return read;
    }
}

/// <summary>2026-11-11-205ff: one turn's stored proposal and when it was last written.</summary>
public sealed record SpecDialogTurnProposalRead(OutcomeProposal Proposal, DateTimeOffset At);
