using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-10-08-e8b9g: stores an approval record and then makes sure it cites nothing the
/// conversation no longer holds. Since uploads can be removed one by one, a removal on another
/// replica may land between the recorder's read of the set ids and its save; the ids are read
/// again AFTER the save and a vanished one is dropped from the record. The removal checks again
/// after ITS write for the other order (ReferenceUploadDeletion).
/// </summary>
public sealed class ApprovalCitations(ISpecApprovalStore store, IReferenceSetReader references)
{
    public async Task<SpecApprovalRecord> SaveAsync(
        SpecApprovalRecord record, string conversation, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(record);
        await store.SaveAsync(record, ct);
        if (record.CitedSets.Count == 0) return record;
        var held = (await references.SetIdsAsync(conversation, ct)).ToHashSet(StringComparer.Ordinal);
        if (record.CitedSets.All(held.Contains)) return record;
        var settled = record with { References = [.. record.CitedSets.Where(held.Contains)] };
        await store.SaveAsync(settled, ct);
        return settled;
    }
}
