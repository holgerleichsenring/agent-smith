using AgentSmith.Infrastructure.Persistence.Entities;

namespace AgentSmith.Infrastructure.Persistence.Services.Prompts;

/// <summary>
/// 2026-11-18-e06bd: a call's prompt as its chain of records and the block each position holds. Each record keeps the
/// first <c>ParentLength</c> blocks of the one before and appends its own. Shared by the prompt reader and the digest.
/// </summary>
internal static class PromptChain
{
    /// <summary>Root first. A parent that is gone ends the walk: what was inherited from it is no longer known.</summary>
    public static List<PromptRecord> Of(
        PromptRecord call, IReadOnlyDictionary<string, PromptRecord> records, out bool complete)
    {
        var chain = new List<PromptRecord> { call };
        var current = call;
        complete = true;
        while (current.ParentId is { } parentId)
        {
            if (!records.TryGetValue(parentId, out var parent) || chain.Contains(parent))
            {
                complete = current.ParentLength == 0;
                break;
            }
            chain.Add(parent);
            current = parent;
        }
        chain.Reverse();
        return chain;
    }

    /// <summary>Position to block hash, for a root-first chain and the blocks its records hold.</summary>
    public static SortedDictionary<int, string> Positions(
        IReadOnlyList<PromptRecord> chain, IReadOnlyCollection<PromptRecordBlock> held)
    {
        var positions = new SortedDictionary<int, string>();
        foreach (var record in chain)
        {
            foreach (var cut in positions.Keys.Where(p => p >= record.ParentLength).ToList()) positions.Remove(cut);
            foreach (var block in held.Where(b => b.RecordId == record.Id)) positions[block.Position] = block.BlockHash;
        }
        return positions;
    }
}
