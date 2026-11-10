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

    /// <summary>The conversation's turn proposals by run id; a row that cannot be read is left out.</summary>
    public async Task<IReadOnlyDictionary<string, OutcomeProposal>> ReadAsync(string sessionId, CancellationToken ct)
    {
        var read = new Dictionary<string, OutcomeProposal>(StringComparer.Ordinal);
        foreach (var (runId, json) in await repository.ReadBySessionAsync(sessionId, ct))
        {
            try
            {
                read[runId] = OutcomeProposalJson.Read(json);
            }
            catch (Exception ex) when (ex is System.Text.Json.JsonException or InvalidOperationException)
            {
                logger.LogWarning(ex, "The proposal of run {RunId} on {SessionId} cannot be read", runId, sessionId);
            }
        }
        return read;
    }
}
