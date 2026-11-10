using AgentSmith.Contracts.Dialogue;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>2026-11-10-14a3d: what the conversation holds that decides a tool — a filing to withdraw.</summary>
public sealed record SpecDialogTurnFacts(IFiledTicketWithdrawal? Withdrawal);

/// <summary>
/// 2026-11-10-14a3d: reads, before a turn, whether its conversation filed anything, so the turn is offered
/// withdraw_filed_ticket only with something to withdraw. A read that fails offers the tool, as before.
/// </summary>
public sealed class SpecDialogTurnMaterial(IFiledTicketWithdrawal withdrawal, IServiceScopeFactory scopes)
{
    public async Task<SpecDialogTurnFacts> OfAsync(string sessionId, CancellationToken ct) =>
        new(await HasFiledAsync(sessionId, ct) ? withdrawal : null);

    private async Task<bool> HasFiledAsync(string sessionId, CancellationToken ct)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<SpecDialogLatestOutcomeStore>();
            return (await store.ReadBySessionAsync(sessionId, ct)).Filing is not null;
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            return true;
        }
    }
}
