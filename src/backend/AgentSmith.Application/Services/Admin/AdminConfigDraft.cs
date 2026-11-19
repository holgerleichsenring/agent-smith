using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Admin;

/// <summary>
/// 2026-11-17-f3a8c: the configuration changes an admin turn drafts — one per kind and id, in the order first drafted,
/// continued from the proposal still awaiting a decision so a turn edits its draft instead of starting over.
/// </summary>
public sealed class AdminConfigDraft(IEnumerable<ConfigChangeItem>? seed = null)
{
    private readonly List<ConfigChangeItem> _items = [.. seed ?? []];
    private readonly bool _seeded = seed?.Any() == true;

    public IReadOnlyList<ConfigChangeItem> Items => _items;

    /// <summary>Puts the change, replacing a drafted change of the same entity in place.</summary>
    public void Put(ConfigChangeItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var at = _items.FindIndex(i => Same(i, item.Kind, item.Id));
        if (at >= 0) _items[at] = item;
        else _items.Add(item);
    }

    /// <summary>2026-11-19-7c42a: the turn dropped every change of a draft it continued — the administrator's discard,
    /// said in words; the proposal awaiting a decision is withdrawn. Unlike a design draft there is no recut that empties
    /// it on the way: changes are put and dropped one entity at a time, so an empty draft at the end is the turn's word.</summary>
    public bool Emptied => _seeded && _items.Count == 0;

    /// <summary>Whether a drafted change of that entity was there to drop.</summary>
    public bool Drop(string kind, string id) => _items.RemoveAll(i => Same(i, kind, id)) > 0;

    private static bool Same(ConfigChangeItem item, string kind, string id) =>
        string.Equals(item.Kind, kind, StringComparison.OrdinalIgnoreCase) && string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase);
}
