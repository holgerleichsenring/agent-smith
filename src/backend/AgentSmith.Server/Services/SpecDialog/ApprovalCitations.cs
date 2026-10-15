using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-10-08-e8b9g: stores an approval record and then makes sure it cites nothing the
/// conversation no longer holds. Since uploads can be removed one by one, a removal on another
/// replica may land between the recorder's read of the set ids and its save; the ids are read
/// again AFTER the save and a vanished one is dropped from the record. The removal checks again
/// after ITS write for the other order (ReferenceUploadDeletion).
/// <para>2026-10-14-dbcel: a later approval KEEPS what the previous record cited from another conversation while
/// that conversation still holds it, recording where each such id lives (CitedFrom); an own id keeps today's rule.</para>
/// </summary>
public sealed class ApprovalCitations(ISpecApprovalStore store, IReferenceSetReader references)
{
    /// <summary>2026-10-08-e8b9k: the record citing the sets and the images the conversation holds now, and
    /// the previous record's carried-forward ones whose own conversation still holds them.</summary>
    public async Task<SpecApprovalRecord> CiteAsync(
        SpecApprovalRecord record, string conversation, CancellationToken ct, SpecApprovalRecord? previous = null)
    {
        var sets = (await references.SetIdsAsync(conversation, ct)).ToList();
        var images = (await references.ImageSetIdsAsync(conversation, ct)).ToList();
        var from = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var source in Foreign(previous, conversation))
        {
            var (heldSets, heldImages) = (await references.SetIdsAsync(source, ct), await references.ImageSetIdsAsync(source, ct));
            foreach (var id in previous!.CitedSets.Where(id => previous.ConversationOf(id) == source && heldSets.Contains(id))) { sets.Add(id); from[id] = source; }
            foreach (var id in previous.CitedImages.Where(id => previous.ConversationOf(id) == source && heldImages.Contains(id))) { images.Add(id); from[id] = source; }
        }
        return record with { References = sets, Images = images, CitedFrom = from.Count == 0 ? null : from };
    }

    public async Task<SpecApprovalRecord> SaveAsync(
        SpecApprovalRecord record, string conversation, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(record);
        await store.SaveAsync(record, ct);
        if (record.CitedSets.Count == 0 && record.CitedImages.Count == 0) return record;
        var sets = (await references.SetIdsAsync(conversation, ct)).ToHashSet(StringComparer.Ordinal);
        var images = (await references.ImageSetIdsAsync(conversation, ct)).ToHashSet(StringComparer.Ordinal);
        bool Kept(string id, HashSet<string> held) => held.Contains(id) || record.CitedFrom?.ContainsKey(id) == true;
        if (record.CitedSets.All(id => Kept(id, sets)) && record.CitedImages.All(id => Kept(id, images))) return record;
        var settled = record with
        {
            References = [.. record.CitedSets.Where(id => Kept(id, sets))],
            Images = [.. record.CitedImages.Where(id => Kept(id, images))],
        };
        await store.SaveAsync(settled, ct);
        return settled;
    }

    private static IEnumerable<string> Foreign(SpecApprovalRecord? previous, string conversation) => previous is null ? [] :
        previous.CitedSets.Concat(previous.CitedImages).Select(id => previous.ConversationOf(id)).OfType<string>()
            .Where(c => c.Length > 0 && !string.Equals(c, conversation, StringComparison.Ordinal)).Distinct(StringComparer.Ordinal);
}
