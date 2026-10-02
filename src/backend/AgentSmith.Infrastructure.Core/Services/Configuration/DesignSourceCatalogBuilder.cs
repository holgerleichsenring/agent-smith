using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// 2026-10-01-7f7aa: converts raw <c>design_sources:</c> entries into <see cref="DesignSource"/>
/// records keyed by <see cref="ConfigNames.Comparer"/>. A source whose auth names no entry of
/// <c>secrets:</c> is one blocking finding and is not built — it would otherwise survive to
/// fail at its first read, after a run had already started on it.
/// </summary>
public sealed class DesignSourceCatalogBuilder
{
    private readonly CatalogKeyCollisions _collisions = new();

    public Dictionary<string, DesignSource> Build(
        IReadOnlyDictionary<string, RawDesignSourceEntry> raw,
        IEnumerable<string> secretNames, List<StartupFinding> findings)
    {
        var dropped = _collisions.Detect("design_sources", raw.Keys, findings);
        var secrets = secretNames.ToHashSet(ConfigNames.Comparer);
        var result = new Dictionary<string, DesignSource>(raw.Count, ConfigNames.Comparer);
        foreach (var (name, entry) in raw)
        {
            if (dropped.Contains(name)) continue;
            if (!secrets.Contains(entry.Auth))
            {
                findings.Add(MissingSecret(name, entry.Auth));
                continue;
            }
            result[name] = new DesignSource(name, entry.Vendor, entry.Auth, entry.DisplayName);
        }
        return result;
    }

    private static StartupFinding MissingSecret(string name, string auth) =>
        new(StartupSubsystems.Configuration, StartupFindingSeverity.Blocking,
            string.IsNullOrWhiteSpace(auth)
                ? $"Design source '{name}' names no secret in 'auth'."
                : $"Design source '{name}' names secret '{auth}', which is not defined in secrets: catalog.",
            Field: $"design_sources:{name}");
}
