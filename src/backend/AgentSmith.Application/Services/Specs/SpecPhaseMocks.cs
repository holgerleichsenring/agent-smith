using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-01-283dh: which files of a spec directory are a phase's design mocks — an .html whose
/// NAME starts with the phase id followed by '.' or '-'. The phase id is the link because it is
/// frozen; the stem after it is minted from the goal and moves with every goal edit.
/// </summary>
public static class SpecPhaseMocks
{
    /// <summary>The repository-relative paths of <paramref name="phaseId"/>'s mocks among <paramref name="listed"/>.</summary>
    public static IReadOnlyList<string> Of(IReadOnlyList<string> listed, SpecSetKey key, string phaseId)
    {
        ArgumentNullException.ThrowIfNull(listed);
        return [.. listed
            .Select(path => path.TrimEnd('/').Split('/', '\\')[^1])
            .Where(name => name.EndsWith(".html", StringComparison.OrdinalIgnoreCase)
                && name.Length > phaseId.Length
                && name.StartsWith(phaseId, StringComparison.Ordinal)
                && name[phaseId.Length] is '.' or '-')
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .Select(name => $"{key.Directory}/{name}")];
    }
}
