using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// p0515: copies the raw <c>agents:</c> map into a catalog keyed by
/// <see cref="ConfigNames.Comparer"/>. The raw map was passed straight through before, so
/// the agents catalog was the one that stayed ordinal — and it is read from two places
/// (the project builder resolving <c>agent:</c> and <c>pipelines[].agent</c>, and the
/// composed config the CLI's <c>--agent</c> reads), which is exactly the split this phase
/// removes: one copy is built and handed to both.
/// <para>
/// Built key by key rather than through the copy constructor: that constructor throws
/// <see cref="ArgumentException"/> on a case collision, and an unhandled throw here is a
/// dead boot instead of a reported configuration fault.
/// </para>
/// </summary>
public sealed class AgentCatalogBuilder
{
    private readonly CatalogKeyCollisions _collisions = new();

    public Dictionary<string, AgentConfig> Build(
        IReadOnlyDictionary<string, AgentConfig> raw, List<StartupFinding> findings)
    {
        var dropped = _collisions.Detect("agents", raw.Keys, findings);
        var result = new Dictionary<string, AgentConfig>(raw.Count, ConfigNames.Comparer);

        foreach (var (name, agent) in raw)
            if (!dropped.Contains(name)) result[name] = agent;

        return result;
    }
}
