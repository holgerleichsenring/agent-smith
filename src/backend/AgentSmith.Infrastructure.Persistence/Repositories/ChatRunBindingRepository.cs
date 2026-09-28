using AgentSmith.Contracts.Models;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// Data access for chat-run bindings over a scoped unit of work. The question and close writes
/// are single UPDATE statements with the expected state in the WHERE clause: every replica
/// follows the open bindings, and a read-then-write would let two of them post the same
/// question or the same outcome.
/// </summary>
public sealed class ChatRunBindingRepository(IUnitOfWork unitOfWork, TimeProvider timeProvider)
{
    public async Task BindAsync(ChatRunBindingFact fact, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(fact);
        unitOfWork.Add(new ChatRunBinding
        {
            RunId = fact.RunId, Platform = fact.Platform, ChannelId = fact.ChannelId,
            ThreadId = fact.ThreadId, RequestedBy = fact.RequestedBy, ReplyEndpoint = fact.ReplyEndpoint,
        });
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ChatRunBindingFact>> ListOpenAsync(CancellationToken ct) =>
        await Open().OrderBy(b => b.Id).Select(b => ToFact(b)).ToListAsync(ct);

    public async Task<ChatRunBindingFact?> FindOpenInThreadAsync(
        string platform, string channelId, string? threadId, CancellationToken ct) =>
        await Open()
            .Where(b => b.Platform == platform && b.ChannelId == channelId && b.ThreadId == threadId)
            .OrderByDescending(b => b.Id).Select(b => ToFact(b)).FirstOrDefaultAsync(ct);

    public async Task<bool> TryRecordQuestionAsync(
        string runId, string questionId, string questionJson, CancellationToken ct) =>
        await Open()
            .Where(b => b.RunId == runId && (b.QuestionId == null || b.QuestionId != questionId))
            .ExecuteUpdateAsync(s => s
                .SetProperty(b => b.QuestionId, questionId)
                .SetProperty(b => b.QuestionJson, questionJson)
                .SetProperty(b => b.UpdatedAt, timeProvider.GetUtcNow()), ct) > 0;

    public async Task<bool> TryCloseAsync(string runId, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        return await Open().Where(b => b.RunId == runId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(b => b.ClosedAt, now)
                .SetProperty(b => b.UpdatedAt, now), ct) > 0;
    }

    private IQueryable<ChatRunBinding> Open() =>
        unitOfWork.Set<ChatRunBinding>().Where(b => b.ClosedAt == null);

    private static ChatRunBindingFact ToFact(ChatRunBinding b) =>
        new(
            b.RunId, b.Platform, b.ChannelId, b.ThreadId, b.RequestedBy, b.ReplyEndpoint,
            b.CreatedAt, b.QuestionId, b.QuestionJson);
}
