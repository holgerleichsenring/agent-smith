using AgentSmith.Contracts.Models;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Repositories;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-09-18-7a05: a design conversation and the operator's own answers to it, deleted
/// together. The two tables are cleared in ONE transaction, the way a run deletion clears its
/// satellites: half a delete would leave answers keyed to a session nothing can reach, and the
/// only route that reads that table resolves its id from a run checkpoint rather than from a
/// URL, so nothing would ever read them again.
/// <para>
/// WHAT IS NOT SWEPT. The tickets a filing created belong to the tracker, and the approved
/// specification set stays: every work ticket the dialog files carries the approved stamp, and
/// a stamped ticket whose set is gone is refused at the spec gate on every trigger — so
/// sweeping it would not lose the set silently, it would make the filed ticket permanently
/// unworkable with no route back.
/// </para>
/// </summary>
public sealed class SpecDialogConversationDeleter(
    IUnitOfWork unitOfWork,
    SpecDialogSessionRepository sessions,
    DialogueAnswerRepository answers) : ISpecDialogConversationDeleter
{
    public async Task DeleteAsync(string sessionId, CancellationToken cancellationToken)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        await answers.DeleteByJobAsync(sessionId, cancellationToken);
        await sessions.DeleteBySessionOnPlatformAsync(
            DispatcherDefaults.PlatformDashboard, sessionId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
