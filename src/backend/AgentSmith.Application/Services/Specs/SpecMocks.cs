using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-01-283dh: which files beside a spec are its design mocks — an .html whose NAME starts
/// with the spec id followed by '.' or '-'. The id is the link because it is frozen; the label
/// after it moves with a relabel. 2026-10-06-03c7d: the spec's own state directory is searched.
/// </summary>
public static class SpecMocks
{
    private const string MockExtension = ".html";

    /// <summary>The repository-relative paths of <paramref name="specId"/>'s mocks among the
    /// entries <paramref name="listed"/> of <paramref name="directory"/>.</summary>
    public static IReadOnlyList<string> Of(IReadOnlyList<string> listed, string directory, string specId)
    {
        ArgumentNullException.ThrowIfNull(listed);
        return [.. listed
            .Select(SeriesPaths.FileName)
            .Where(name => IsMock(name) && SeriesPaths.BelongsTo(name, specId))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .Select(name => $"{directory}/{name}")];
    }

    /// <summary>True when <paramref name="fileName"/> is a design mock.</summary>
    public static bool IsMock(string fileName) =>
        fileName.EndsWith(MockExtension, StringComparison.OrdinalIgnoreCase);
}
