using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// p0327: IDialogueAnswerInbox facade for singleton callers (the durable
/// dialogue transport, the resume sweeper). Scope per operation, like
/// <see cref="DbCapacityQueue"/>.
/// </summary>
public sealed class DbDialogueAnswerInbox(IServiceScopeFactory scopeFactory) : IDialogueAnswerInbox
{
    public async Task<bool> TryDeliverAsync(string dialogueJobId, DialogAnswer answer, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<DialogueAnswerRepository>()
            .TryDeliverAsync(dialogueJobId, answer, ct);
    }

    public async Task<DialogAnswer?> GetAsync(string dialogueJobId, string questionId, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<DialogueAnswerRepository>()
            .GetAsync(dialogueJobId, questionId, ct);
    }
}
