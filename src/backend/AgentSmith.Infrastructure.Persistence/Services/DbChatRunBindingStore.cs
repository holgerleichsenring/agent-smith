using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// IChatRunBindingStore facade for singleton callers (the chat-run follower on every replica).
/// Opens a scope per operation and delegates to the scoped repository.
/// </summary>
public sealed class DbChatRunBindingStore(IServiceScopeFactory scopeFactory) : IChatRunBindingStore
{
    public Task BindAsync(ChatRunBindingFact binding, CancellationToken cancellationToken) =>
        InScope(r => r.BindAsync(binding, cancellationToken));

    public Task<IReadOnlyList<ChatRunBindingFact>> ListOpenAsync(CancellationToken cancellationToken) =>
        InScope(r => r.ListOpenAsync(cancellationToken));

    public Task<ChatRunBindingFact?> FindOpenInThreadAsync(
        string platform, string channelId, string? threadId, CancellationToken cancellationToken) =>
        InScope(r => r.FindOpenInThreadAsync(platform, channelId, threadId, cancellationToken));

    public Task<bool> TryRecordQuestionAsync(
        string runId, string questionId, string questionJson, CancellationToken cancellationToken) =>
        InScope(r => r.TryRecordQuestionAsync(runId, questionId, questionJson, cancellationToken));

    public Task<bool> TryCloseAsync(string runId, CancellationToken cancellationToken) =>
        InScope(r => r.TryCloseAsync(runId, cancellationToken));

    private async Task InScope(Func<ChatRunBindingRepository, Task> operation)
    {
        using var scope = scopeFactory.CreateScope();
        await operation(scope.ServiceProvider.GetRequiredService<ChatRunBindingRepository>());
    }

    private async Task<T> InScope<T>(Func<ChatRunBindingRepository, Task<T>> operation)
    {
        using var scope = scopeFactory.CreateScope();
        return await operation(scope.ServiceProvider.GetRequiredService<ChatRunBindingRepository>());
    }
}
