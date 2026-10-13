using AgentSmith.Contracts.Models.Structure;

namespace AgentSmith.Application.Services.Structure;

/// <summary>
/// 2026-10-13-50c7: the entries a round writes. A first baseline lists every excess; a refresh
/// keeps what is still over and adds what a changed limit exposes (2026-10-13-2d0c); a preserved
/// baseline keeps its entries as they are. Either way every excess under a directory the context
/// newly owns is added — it is green on the day it is owned, as a new limit's excess is.
/// </summary>
public sealed class StructureBaselineEntries(StructureBaselineFile baselineFile)
{
    public IReadOnlyList<StructureBaselineEntry> Compose(string? existing, bool preserved,
        IReadOnlyList<StructureFinding> excess, Func<string, bool> isNewlyOwned)
    {
        ArgumentNullException.ThrowIfNull(isNewlyOwned);
        if (existing is null) return [.. excess.Select(StructureBaselineEntry.Of)];
        var old = baselineFile.Parse(existing);
        var kept = preserved ? old : baselineFile.Refresh(old, excess, baselineFile.MeasuredWith(existing));
        var newlyOwned = excess.Where(f => isNewlyOwned(f.Path)).Select(StructureBaselineEntry.Of);
        return [.. kept.Concat(newlyOwned).DistinctBy(e => e.Identity)];
    }
}
