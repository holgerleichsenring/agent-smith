using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>2026-11-10-14a3d: what the conversation holds that decides a tool — a filing to withdraw, a mock to revise.</summary>
public sealed record SpecDialogTurnFacts(IFiledTicketWithdrawal? Withdrawal, bool HasMocks);

/// <summary>
/// 2026-11-10-14a3d: reads, before a turn, whether its conversation filed anything and holds a mock, so the turn is
/// offered withdraw_filed_ticket only with something to withdraw and the mock tools only with a mock (or another
/// visual) to work on. A read that fails offers the tool, as before.
/// </summary>
public sealed class SpecDialogTurnMaterial(
    IFiledTicketWithdrawal withdrawal, IServiceScopeFactory scopes, IConversationMockReader? mocks = null)
{
    public async Task<SpecDialogTurnFacts> OfAsync(string sessionId, CancellationToken ct) =>
        new(await HasFiledAsync(sessionId, ct) ? withdrawal : null, await HasMocksAsync(sessionId, ct));

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

    private async Task<bool> HasMocksAsync(string sessionId, CancellationToken ct)
    {
        if (mocks is null) return false;
        try
        {
            return (await mocks.ListAsync(sessionId, ct)).Count > 0;
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            return true;
        }
    }
}
