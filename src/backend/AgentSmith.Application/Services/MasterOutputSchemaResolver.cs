using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services;

/// <summary>
/// p0267: resolves a master skill's declared <c>output_schema</c> from the loaded
/// skill catalog. Loads the master role definitions once (lazy, double-checked) the
/// same way <c>SkillCatalogPromptCatalog</c> resolves master bodies — from
/// <c>{catalogRoot}/skills</c> via <see cref="ISkillLoader"/> — and caches the
/// name → schema map. Returns null when the catalog is not yet bootstrapped so the
/// caller skips the scrape cleanly instead of throwing.
/// </summary>
public sealed class MasterOutputSchemaResolver(
    ISkillLoader skillLoader,
    ISkillsCatalogPath catalogPath,
    ILogger<MasterOutputSchemaResolver> logger) : IMasterOutputSchemaResolver
{
    private const string CatalogSkillsRootSubPath = "skills";
    private const string MasterRole = "master";

    private readonly object _lock = new();
    private IReadOnlyDictionary<string, string?>? _schemas;

    public string? Resolve(string masterSkillName)
    {
        if (string.IsNullOrWhiteSpace(masterSkillName)) return null;
        var schemas = GetSchemas();
        return schemas is not null && schemas.TryGetValue(masterSkillName, out var schema)
            ? schema
            : null;
    }

    private IReadOnlyDictionary<string, string?>? GetSchemas()
    {
        if (_schemas is not null) return _schemas;
        lock (_lock)
        {
            if (_schemas is not null) return _schemas;
            try
            {
                var skillsRoot = Path.Combine(catalogPath.Root, CatalogSkillsRootSubPath);
                _schemas = skillLoader.LoadRoleDefinitions(skillsRoot)
                    .Where(s => string.Equals(s.Role, MasterRole, StringComparison.Ordinal))
                    .ToDictionary(s => s.Name, s => s.OutputSchema, StringComparer.Ordinal);
                logger.LogDebug(
                    "MasterOutputSchemaResolver loaded {Count} master output schemas", _schemas.Count);
                return _schemas;
            }
            catch (InvalidOperationException ex)
            {
                logger.LogDebug(ex, "Skill catalog not yet bootstrapped; no master schemas resolvable");
                return null;
            }
        }
    }
}
