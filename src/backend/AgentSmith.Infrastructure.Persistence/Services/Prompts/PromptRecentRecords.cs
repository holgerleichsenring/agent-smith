using System.Collections.Concurrent;

namespace AgentSmith.Infrastructure.Persistence.Services.Prompts;

/// <summary>
/// 2026-11-09-0620a: the last records this process stored per run, so the next call of a tool loop finds the record
/// its messages extend. A run holds many, because a master and its sub-agents interleave; the map holds a bounded
/// number of runs, oldest out first. Losing an entry costs a full store, never a wrong one: a parent is only taken when
/// its blocks are exactly the head of the new call.
/// </summary>
public sealed class PromptRecentRecords
{
    // 2026-11-09-0620l: room for a master and a wide fan-out of children interleaving in one run.
    private const int PerRun = 64;
    private const int Runs = 256;

    private readonly ConcurrentDictionary<string, List<(string Id, string[] Hashes)>> _byRun = new();
    private readonly ConcurrentQueue<string> _order = new();

    public (string Id, int Length)? LongestPrefixOf(string runId, IReadOnlyList<string> hashes)
    {
        if (!_byRun.TryGetValue(runId, out var records)) return null;
        lock (records)
        {
            var best = records
                .Where(r => r.Hashes.Length < hashes.Count && IsPrefix(r.Hashes, hashes))
                .OrderByDescending(r => r.Hashes.Length)
                .FirstOrDefault();
            return best.Id is null ? null : (best.Id, best.Hashes.Length);
        }
    }

    public void Remember(string runId, string id, IReadOnlyList<string> hashes)
    {
        var records = _byRun.GetOrAdd(runId, key =>
        {
            _order.Enqueue(key);
            while (_order.Count > Runs && _order.TryDequeue(out var oldest)) _byRun.TryRemove(oldest, out _);
            return [];
        });
        lock (records)
        {
            records.Add((id, [.. hashes]));
            if (records.Count > PerRun) records.RemoveAt(0);
        }
    }

    private static bool IsPrefix(string[] head, IReadOnlyList<string> whole)
    {
        for (var i = 0; i < head.Length; i++)
            if (!string.Equals(head[i], whole[i], StringComparison.Ordinal)) return false;
        return true;
    }
}
