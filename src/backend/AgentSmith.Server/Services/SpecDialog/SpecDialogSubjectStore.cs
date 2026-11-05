using AgentSmith.Infrastructure.Persistence.Repositories;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-11-04-96a0a: stores the subject the design master named a conversation with. Written straight to the row, and
/// only while it still has none: the state was read before the turn ran, so the row is what says whether one was
/// stored meanwhile, and a stored subject is never changed.
/// </summary>
public sealed class SpecDialogSubjectStore(SpecDialogSessionRepository repository)
{
    public async Task StoreAsync(string platform, string threadId, string subject, CancellationToken ct)
    {
        var session = await repository.GetOpenByThreadAsync(platform, threadId, ct);
        if (session is null || session.Subject is not null) return;
        session.Subject = subject;
        await repository.SaveAsync(ct);
    }
}
