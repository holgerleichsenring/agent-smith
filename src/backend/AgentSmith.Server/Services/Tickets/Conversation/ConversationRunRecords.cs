using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Services.Tickets.Conversation;

/// <summary>
/// 2026-10-13-9a59h: what a ticket's attempts recorded of its conversation, in one scope — the conversation
/// rows (ticket text, questions, hand-backs, cut reviews), the attempt rows (fingerprint, worked approval)
/// and the answers in the dialogue inbox, whose job id is the run id.
/// </summary>
public sealed class ConversationRunRecords(IServiceScopeFactory scopes)
{
    public async Task<ConversationRunRows> ReadAsync(IReadOnlyList<string> runIds, CancellationToken ct)
    {
        if (runIds.Count == 0) return new([], [], []);
        using var scope = scopes.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        return new ConversationRunRows(
            await uow.Set<RunConversationEntry>().AsNoTracking().Where(e => runIds.Contains(e.RunId)).ToListAsync(ct),
            await uow.Set<RunAttempt>().AsNoTracking().Where(a => runIds.Contains(a.RunId)).ToListAsync(ct),
            await uow.Set<DialogueAnswerEntry>().AsNoTracking().Where(a => runIds.Contains(a.DialogueJobId)).ToListAsync(ct));
    }
}

/// <summary>2026-10-13-9a59h: the rows <see cref="ConversationRunRecords"/> read.</summary>
public sealed record ConversationRunRows(
    IReadOnlyList<RunConversationEntry> Entries,
    IReadOnlyList<RunAttempt> Attempts,
    IReadOnlyList<DialogueAnswerEntry> Answers);
