using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>2026-11-10-14a3d: what the conversation holds that decides a tool — a filing to withdraw.
/// 2026-11-12-cd4ck: and its notes store with the notes as the turn begins.</summary>
public sealed record SpecDialogTurnFacts(
    IFiledTicketWithdrawal? Withdrawal, IConversationNotes? NotesStore = null, string? Notes = null);

/// <summary>
/// 2026-11-10-14a3d: reads, before a turn, whether its conversation filed anything, so the turn is offered
/// withdraw_filed_ticket only with something to withdraw. A read that fails offers the tool, as before.
/// 2026-11-12-cd4ck: and reads the conversation's notes, which the turn's prompt carries whole.
/// </summary>
public sealed class SpecDialogTurnMaterial(
    IFiledTicketWithdrawal withdrawal, IServiceScopeFactory scopes, IConversationNotes? notes = null)
{
    public async Task<SpecDialogTurnFacts> OfAsync(string sessionId, CancellationToken ct) =>
        new(await HasFiledAsync(sessionId, ct) ? withdrawal : null, notes,
            notes is null ? null : await notes.ReadAsync(sessionId, ct));

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
