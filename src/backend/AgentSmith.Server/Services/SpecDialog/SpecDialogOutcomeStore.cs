using AgentSmith.Contracts.Models;
using AgentSmith.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// p0315e: persists the CONFIRMED outcome proposal on the open spec-dialog
/// session — the durable handoff p0315c files tickets from. Scoped, over the
/// same relational unit of work as the session manager.
/// </summary>
public sealed class SpecDialogOutcomeStore(
    SpecDialogSessionRepository repository,
    ILogger<SpecDialogOutcomeStore> logger)
{
    public async Task SetConfirmedAsync(
        string platform, string threadId, OutcomeProposal proposal, CancellationToken ct)
    {
        var session = await repository.GetOpenByThreadAsync(platform, threadId, ct)
            ?? throw new InvalidOperationException(
                $"No open spec-dialog session on thread '{threadId}' ({platform}) "
                + "to store the confirmed outcome on.");
        session.ConfirmedOutcomeJson = OutcomeProposalJson.Write(proposal);
        await repository.SaveAsync(ct);
        logger.LogInformation(
            "Stored confirmed outcome on spec-dialog session {SessionId}", session.SessionId);
    }
}
