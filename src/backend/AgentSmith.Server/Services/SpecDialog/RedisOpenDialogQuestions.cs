using AgentSmith.Server.Contracts;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-10-26-0f19b: <see cref="IOpenDialogQuestions"/> as one Redis key per session holding the open
/// question's id, expiring with the wait. A close removes the key only while it still names the question
/// it closes. The record only lets a click on another replica through — the asking replica's buttons and
/// the thread's text replies answer without it — so a failed open or close is logged, never thrown.
/// </summary>
public sealed class RedisOpenDialogQuestions(IConnectionMultiplexer redis, ILogger<RedisOpenDialogQuestions> logger)
    : IOpenDialogQuestions
{
    public Task OpenAsync(string sessionId, string questionId, TimeSpan waitsFor, CancellationToken cancellationToken) =>
        LoggedAsync(sessionId, () => redis.GetDatabase().StringSetAsync(Key(sessionId), questionId, waitsFor));

    public Task CloseAsync(string sessionId, string questionId, CancellationToken cancellationToken) =>
        LoggedAsync(sessionId, async () =>
        {
            if (await IsOpenAsync(sessionId, questionId, cancellationToken))
                await redis.GetDatabase().KeyDeleteAsync(Key(sessionId));
        });

    public async Task<bool> IsOpenAsync(string sessionId, string questionId, CancellationToken cancellationToken) =>
        await redis.GetDatabase().StringGetAsync(Key(sessionId)) == questionId;

    private async Task LoggedAsync(string sessionId, Func<Task> write)
    {
        try { await write(); }
        catch (Exception ex) when (ex is RedisException or TimeoutException)
        {
            logger.LogWarning(ex, "Open-question record for spec-dialog session {SessionId} unavailable — "
                + "its buttons answer on the replica that asked only", sessionId);
        }
    }

    private static string Key(string sessionId) => $"spec-dialog:{sessionId}:open-question";
}
