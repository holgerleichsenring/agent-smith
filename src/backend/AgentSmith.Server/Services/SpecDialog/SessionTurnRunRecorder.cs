using AgentSmith.Contracts.Turns;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-11-10-d854c: writes the running turn's run id onto its conversation's row, in a scope of its own because the
/// turn's scope is in use around it. One column, written in place, so nothing else on the row is touched; it never
/// fails the turn it reports on.
/// </summary>
public sealed class SessionTurnRunRecorder(
    IServiceScopeFactory scopes, ConversationState state, ILogger<SessionTurnRunRecorder> logger) : ITurnRunRecorder
{
    public async Task RecordAsync(string runId, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().Set<SpecDialogSession>()
                .Where(s => s.SessionId == state.JobId)
                .ExecuteUpdateAsync(row => row.SetProperty(s => s.RunningRunId, runId), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not record run {RunId} as the running turn of {SessionId}", runId, state.JobId);
        }
    }
}
