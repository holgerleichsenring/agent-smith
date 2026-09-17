using AgentSmith.Contracts.Models;
using AgentSmith.Server.Hubs;
using AgentSmith.Server.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-09-15-6d9c: delivers a turn's outcome to the one dashboard session holding the
/// conversation — what WOULD be filed while the person is still deciding, and what WAS
/// filed once they approved.
/// <para>
/// The dashboard platform only. A Slack or Teams session reads its outcome in the thread
/// it was proposed in, and its thread id is a dialog id nobody has joined.
/// </para>
/// <para>
/// The hub context is OPTIONAL for the reason DashboardAdapter's is: the dashboard API is
/// env-gated while the spec-dialog services are composed unconditionally, and a push that
/// finds no hub says so rather than throwing inside a design turn.
/// </para>
/// </summary>
public sealed class DashboardOutcomeChannel(
    SpecDialogProposalComposer composer,
    ILogger<DashboardOutcomeChannel> logger,
    IHubContext<JobsHub>? hub = null)
{
    private const string ProposalMethod = "SpecDialogProposal";
    private const string FiledMethod = "SpecDialogFiled";

    /// <summary>What this turn would file, published before the person is asked.</summary>
    public Task ProposeAsync(
        ConversationState state, OutcomeProposal proposal, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!DialogTarget.IsDashboard(state)) return Task.CompletedTask;
        var dialogId = DialogTarget.Of(state);
        var push = composer.Compose(dialogId, proposal, DateTimeOffset.UtcNow);
        return push is null
            ? Task.CompletedTask
            : PushAsync(ProposalMethod, dialogId, push, cancellationToken);
    }

    /// <summary>
    /// What the filing attempt created. Published on failure too — the report names what
    /// WAS created before it stopped, and a pane that hid it would lose those tickets.
    /// </summary>
    public Task FiledAsync(
        ConversationState state, FilingReport report, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(report);
        if (!DialogTarget.IsDashboard(state)) return Task.CompletedTask;
        var dialogId = DialogTarget.Of(state);
        return PushAsync(
            FiledMethod, dialogId,
            new SpecDialogFilingPush(dialogId, report.Filed, report.Error, DateTimeOffset.UtcNow, report.Notes),
            cancellationToken);
    }

    private async Task PushAsync(
        string clientMethod, string dialogId, object payload, CancellationToken cancellationToken)
    {
        if (hub is null)
        {
            logger.LogWarning(
                "The dashboard API is switched off, so '{ClientMethod}' for spec dialog "
                + "{DialogId} has nowhere to go", clientMethod, dialogId);
            return;
        }
        await hub.Clients.Group(HubGroups.SpecDialog(dialogId))
            .SendAsync(clientMethod, payload, cancellationToken);
    }
}
