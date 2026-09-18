using AgentSmith.Contracts.Specs;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// 2026-09-17-0e79a: data access for the approved record over a scoped unit of work. One row
/// per spec key — approving the same ticket again upserts in place, because the row answers
/// "what is approved for this ticket now", and the branch history answers "what happened".
/// </summary>
public sealed class ApprovedSpecSetRepository(IUnitOfWork unitOfWork)
{
    public async Task<SpecApprovalRecord?> GetAsync(string tracker, string key, CancellationToken ct)
    {
        var row = await unitOfWork.Set<ApprovedSpecSet>().AsNoTracking()
            .FirstOrDefaultAsync(a => a.Tracker == tracker && a.SpecKey == key, ct);
        return row is null ? null : SpecApprovalJson.Read(row.RecordJson);
    }

    public async Task SaveAsync(SpecApprovalRecord record, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(record);
        var existing = await unitOfWork.Set<ApprovedSpecSet>()
            .FirstOrDefaultAsync(a => a.Tracker == record.Tracker && a.SpecKey == record.Key, ct);
        if (existing is null)
        {
            existing = new ApprovedSpecSet { SpecKey = record.Key, Tracker = record.Tracker };
            unitOfWork.Add(existing);
        }
        Apply(record, existing);
        await unitOfWork.SaveChangesAsync(ct);
    }

    private static void Apply(SpecApprovalRecord record, ApprovedSpecSet row)
    {
        row.RecordJson = SpecApprovalJson.Write(record);
        row.ApprovedAt = record.Approval?.At ?? default;
        row.ApprovedInConversation = record.Approval?.Conversation ?? string.Empty;
        row.ApprovedBy = record.Approval?.Principal ?? string.Empty;
    }
}
