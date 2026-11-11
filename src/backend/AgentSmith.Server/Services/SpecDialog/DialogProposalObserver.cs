using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Turns;
using AgentSmith.Server.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-11-09-9a98: stores a proposal under review on the session row and pushes it to the page.
/// The row is what survives a cancel and what every replica reads. It writes in a scope of its
/// own, because the turn's scope is in use around it, and it never fails the review it reports on.
/// <para>2026-11-10-ccd9: it writes only the reviewing column and pushes the draft without an id, so
/// the proposal awaiting a decision stays as it was and the draft cannot be approved.</para>
/// </summary>
public sealed class DialogProposalObserver(
    IServiceScopeFactory scopes, ConversationState state, ILogger<DialogProposalObserver> logger)
    : ITurnProposalObserver
{
    public async Task PublishAsync(OutcomeProposal proposal, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var services = scope.ServiceProvider;
            await services.GetRequiredService<SpecDialogProposalState>()
                .StoreReviewingAsync(state, proposal, cancellationToken);
            // 2026-11-10-d854c: and on the turn's own row, so a turn that dies here still leaves this draft.
            await services.GetRequiredService<SpecDialogTurnProposalStore>().WriteAsync(
                state, services.GetRequiredService<IRunContextAccessor>().CurrentRunId, proposal, cancellationToken);
            await services.GetRequiredService<DashboardOutcomeChannel>()
                .ProposeAsync(state, proposal, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not publish the proposal under review of {SessionId}", state.JobId);
        }
    }
}
