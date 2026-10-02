using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ClarificationRow = AgentSmith.Infrastructure.Persistence.Entities.PendingClarification;

namespace AgentSmith.Server.Services;

/// <summary>
/// Holds pending clarifications: a low-confidence chat command waits here for the person to
/// confirm it before anything is launched, for two hours.
/// 2026-10-02-5ab2d: in the database, not Redis — a flush must not lose the operator's pending
/// command, and the two hours are a column. A take removes what it returns in one conditional
/// delete, so a double click dispatches once even on two replicas.
/// </summary>
public sealed class ClarificationStateManager(
    IServiceScopeFactory scopes,
    TimeProvider timeProvider,
    ILogger<ClarificationStateManager> logger)
{
    private static readonly TimeSpan PendingFor = TimeSpan.FromHours(2);

    public async Task SetAsync(
        string platform, string channelId, PendingClarification pending,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pending);
        var now = timeProvider.GetUtcNow();
        using var scope = scopes.CreateScope();
        await scope.ServiceProvider.GetRequiredService<PendingClarificationRepository>().SetAsync(new ClarificationRow
        {
            Platform = platform, ChannelId = channelId, SuggestedText = pending.SuggestedText,
            OriginalInput = pending.OriginalInput, UserId = pending.UserId,
            ExpiresAt = now + PendingFor, Token = Guid.NewGuid().ToString("N"), UpdatedAt = now,
        }, cancellationToken);
        logger.LogDebug("Stored pending clarification for {ChannelId}", channelId);
    }

    /// <summary>The channel's pending command, removed so no other click can take it; null when
    /// none is pending — never set, already taken, or expired. Each reads the same: nothing runs.</summary>
    public async Task<PendingClarification?> TakeAsync(
        string platform, string channelId, CancellationToken cancellationToken)
    {
        using var scope = scopes.CreateScope();
        var row = await scope.ServiceProvider.GetRequiredService<PendingClarificationRepository>()
            .TakeAsync(platform, channelId, timeProvider.GetUtcNow(), cancellationToken);
        logger.LogDebug("Took pending clarification for {ChannelId}: {Found}", channelId, row is not null);
        return row is null ? null : new PendingClarification(row.SuggestedText, row.OriginalInput, row.UserId);
    }
}
