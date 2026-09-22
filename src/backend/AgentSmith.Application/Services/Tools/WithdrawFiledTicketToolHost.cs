using System.ComponentModel;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Dialogue;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-09-22-9519: withdraw_filed_ticket — the design turn's one act on a tracker. It closes a
/// ticket THIS conversation filed and nothing has claimed, and answers what the tracker did.
/// <para>
/// The conversation is not a parameter. The session id is the dialogue identity the turn was
/// seeded with, exactly as ask_human's job id is, so a turn can only ever withdraw from its own
/// filing and the ticket is named by the display key the filing reported.
/// </para>
/// </summary>
public sealed class WithdrawFiledTicketToolHost(
    IFiledTicketWithdrawal withdrawal, string sessionId) : IToolHost
{
    public IEnumerable<AIFunction> GetTools(SkillExecutionPhase? phase, string? investigatorMode)
    {
        _ = phase;
        _ = investigatorMode;
        return [AIFunctionFactory.Create(Withdraw, name: "withdraw_filed_ticket")];
    }

    [Description(
        "Closes a ticket THIS conversation filed, when nothing has claimed it yet, and reports "
        + "what the tracker did. Refused while any run holds the ticket — stop that run from the "
        + "run controls instead. The reply says whether the ticket actually closed.")]
    public async Task<string> Withdraw(
        [Description("The display key of the filed ticket, exactly as the filing reported it.")] string key,
        [Description("Why it is being withdrawn. Posted onto the ticket as the resolution.")] string reason = "",
        CancellationToken ct = default)
    {
        var result = await withdrawal.WithdrawAsync(sessionId, key, reason, ct);
        return result.Withdrawn ? $"Withdrawn: {result.Reason}" : $"Not withdrawn: {result.Reason}";
    }
}
