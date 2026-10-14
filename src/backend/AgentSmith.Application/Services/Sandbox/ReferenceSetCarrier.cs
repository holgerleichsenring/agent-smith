using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;
using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// 2026-10-01-283df: writes the website sets an approval cites into the carrying repository's
/// sandbox, each in a directory named by its id — so two sets named alike never collide — and
/// keeps that directory out of git. Every set is READ before any is written: a set this process
/// cannot read refuses the whole carry, because building silently without the approved reference
/// is the failure this series removes.
/// 2026-10-02-075dd: each set carries its note as it stands when the run begins.
/// </summary>
public sealed class ReferenceSetCarrier(
    IReferenceSetReader sets, ReferenceSetMaterialiser materialiser, ReferenceGitExclusion exclusion,
    IReferenceNotes? notes = null)
{
    internal const string NoStoreReason =
        "this process holds no reference store, or the set was deleted";

    public async Task<ReferenceCarry> CarryAsync(
        SpecApprovalRecord record, string repo, ISandbox sandbox, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(record);
        // 2026-10-14-dbcel: each id is read from the conversation it was uploaded in — a carried-forward one's own.
        if (record.CitedSets.Any(id => string.IsNullOrEmpty(record.ConversationOf(id))))
            return ReferenceCarry.Refused(Refusal(record.CitedSets, "the approval names no conversation to read them from"));
        var read = new List<(string Id, IReadOnlyList<ReferenceSetFile> Files)>();
        foreach (var id in record.CitedSets) read.Add((id, await sets.FilesAsync(record.ConversationOf(id)!, id, ct)));
        if (read.Where(r => r.Files.Count == 0).Select(r => r.Id).ToList() is { Count: > 0 } unreadable)
            return ReferenceCarry.Refused(Refusal(unreadable, NoStoreReason));
        await exclusion.EnsureAsync(sandbox, ct);
        var addresses = ReferenceScopeName.For(read.Select(r => ReferenceSetName.Of(r.Files.Select(f => f.Path))));
        var noted = new Dictionary<string, string>();
        foreach (var session in notes is null ? [] : record.CitedSets.Select(id => record.ConversationOf(id)!).Distinct())
            foreach (var (id, note) in await notes!.NotesAsync(session, ct)) noted[id] = note;
        var carried = new List<CarriedReferenceSet>();
        for (var i = 0; i < read.Count; i++)
        {
            var (id, files) = read[i];
            var path = ReferenceDirectory.ForSet(id);
            await materialiser.WriteUnderAsync(sandbox, files, path, ct);
            carried.Add(new CarriedReferenceSet(id, ReferenceSetName.Of(files.Select(f => f.Path)),
                addresses[i], repo, path, record.ConversationOf(id)!, files.Count, noted.GetValueOrDefault(id)));
        }
        return new ReferenceCarry(carried, null);
    }

    internal static string Refusal(IReadOnlyList<string> setIds, string reason) =>
        $"The approval cites uploaded website set(s) {string.Join(", ", setIds)} that this run cannot "
        + $"read: {reason}. A run does not build without the reference its approval names.";
}
