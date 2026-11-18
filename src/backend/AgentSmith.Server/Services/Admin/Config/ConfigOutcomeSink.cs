using AgentSmith.Application.Services.Admin;
using AgentSmith.Contracts.Models;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Admin.Config;

/// <summary>
/// 2026-11-17-f3a8c: Apply on the admin assistant's drafted changes — a config run for the conversation's owner, the
/// one person who may decide on it. Applied clears the confirmed slot and is kept as a filing turn; refused goes back
/// to awaiting, with the reason in the conversation, so the assistant can revise the draft.
/// 2026-11-18-78efa: a draft deleting a stored secret value is refused before any run for a decider without
/// secrets.write — secret rights stay with secrets — and the run checks the grant again.
/// </summary>
public sealed class ConfigOutcomeSink(
    IConfigApplyRun apply, DialogDeciderGrants decider, SpecDialogConfirmedSlot confirmed, SpecDialogMessenger messenger,
    SpecDialogSessionManager sessions, ILogger<ConfigOutcomeSink> logger) : IOutcomeSink
{
    public async Task<OutcomeAcceptance> AcceptAsync(
        ConversationState state, OutcomeProposal proposal, bool mayStartRuns,
        CancellationToken cancellationToken, string? proposalId = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (proposal is not ConfigChangeOutcome changes)
            throw new InvalidOperationException($"The config sink applies configuration changes, not {proposal.GetType().Name}.");
        var report = SecretValuesApproval.Refusal(changes.Changes, decider.MayWriteSecrets) is { } refused
            ? new ConfigApplyReport(false, refused)
            : await apply.RunAsync(changes, state.UserId, decider.MayWriteSecrets);
        var ct = report.Applied ? CancellationToken.None : cancellationToken;
        if (report.Applied) await confirmed.ClearAsync(state.Platform, state.ThreadId!, proposalId, ct);
        logger.LogInformation("Admin conversation {SessionId}: {Line}", state.JobId, report.Line);
        await messenger.SendAsync(state.Platform, state.ChannelId, state.ThreadId!, report.Line, ct);
        await sessions.AppendTurnAsync(state.Platform, state.ThreadId!, TranscriptRole.Assistant, report.Line,
            report.Applied ? SpecDialogTurnKind.Filing : SpecDialogTurnKind.Notice, null, ct);
        return report.Applied ? OutcomeAcceptance.Acted : OutcomeAcceptance.Refused;
    }
}
