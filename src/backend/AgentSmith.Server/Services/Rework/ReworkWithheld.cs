using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Services.Rework;

/// <summary>
/// 2026-10-08-0781: acts an operator's stop withholds — written at or before the ticket's
/// notServedThrough watermark (a cancel or a delete). Such an act is not served; it is said once.
/// </summary>
public sealed class ReworkWithheld(IServiceScopeFactory scopes, ReworkSpeech speech)
{
    public async Task<bool> IsWithheldAsync(ResolvedProject project, string ticketId, PendingReworkAct act, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var mark = await scope.ServiceProvider.GetRequiredService<ReworkLedgerRepository>().WatermarkAsync(project.Name, ticketId, ct);
        return mark is { } through && act.Act.At <= through;
    }

    public async Task<bool> SaysAsync(
        ResolvedProject project, string ticketId, PendingReworkAct act, PreviousAttempt attempt, CancellationToken ct)
    {
        if (!await IsWithheldAsync(project, ticketId, act, ct)) return false;
        await speech.SayOnceAsync(project, ticketId, act, attempt.RunId, "withheld",
            ReworkTexts.TicketWithheld(attempt.RunId), ReworkTexts.PrWithheld(ticketId, attempt.RunId), ct);
        return true;
    }
}
