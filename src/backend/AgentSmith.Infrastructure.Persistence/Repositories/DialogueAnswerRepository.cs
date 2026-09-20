using AgentSmith.Contracts.Dialogue;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// p0327: data access for the durable answer inbox over a scoped unit of work.
/// The unique (DialogueJobId, QuestionId) index enforces first-answer-wins; a
/// losing insert is translated to false, never an exception.
/// </summary>
public sealed class DialogueAnswerRepository(
    IUnitOfWork unitOfWork,
    IUniqueViolationTranslator violationTranslator)
{
    public async Task<bool> TryDeliverAsync(string dialogueJobId, DialogAnswer answer, CancellationToken ct)
    {
        var entry = unitOfWork.Add(new DialogueAnswerEntry
        {
            DialogueJobId = dialogueJobId,
            QuestionId = answer.QuestionId,
            Answer = answer.Answer,
            Comment = answer.Comment,
            AnsweredBy = answer.AnsweredBy,
            AnsweredAt = answer.AnsweredAt,
        });
        try
        {
            await unitOfWork.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException ex) when (violationTranslator.IsUniqueViolation(ex))
        {
            // First answer wins — this one lost the slot.
            entry.State = Microsoft.EntityFrameworkCore.EntityState.Detached;
            return false;
        }
    }

    /// <summary>
    /// 2026-09-18-7a05: every answer stored against one dialogue identity. A design
    /// conversation's session id IS that identity, so deleting the conversation takes the
    /// operator's own answers with it — inside the caller's transaction.
    /// </summary>
    public Task<int> DeleteByJobAsync(string dialogueJobId, CancellationToken ct) =>
        unitOfWork.Set<DialogueAnswerEntry>()
            .Where(a => a.DialogueJobId == dialogueJobId)
            .ExecuteDeleteAsync(ct);

    public async Task<DialogAnswer?> GetAsync(string dialogueJobId, string questionId, CancellationToken ct)
    {
        var row = await unitOfWork.Set<DialogueAnswerEntry>().AsNoTracking()
            .FirstOrDefaultAsync(a => a.DialogueJobId == dialogueJobId && a.QuestionId == questionId, ct);
        return row is null
            ? null
            : new DialogAnswer(row.QuestionId, row.Answer, row.Comment, row.AnsweredAt, row.AnsweredBy);
    }
}
