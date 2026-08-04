using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0399: a revision is a FULL REPLACE of the spec directory. Revision 3's six phases
/// committed beside revision 2's monolith is two truths in one directory — exactly
/// what "one artifact" was meant to end. Selects the spec files a revision leaves
/// behind: every yaml/markdown in the directory that is not part of the current cut.
/// The index and the accounting are part of every cut and always survive.
/// </summary>
internal static class SpecSetStaleFiles
{
    public static IReadOnlyList<string> Select(
        IReadOnlyList<string> listed, SpecSetKey key, SpecSet set)
    {
        var current = CurrentCut(key, set);
        return [.. listed
            .Select(FileName)
            .Where(name => IsSpecFile(name) && !current.Contains(name))
            .Distinct(StringComparer.Ordinal)
            .Select(name => $"{key.Directory}/{name}")];
    }

    private static HashSet<string> CurrentCut(SpecSetKey key, SpecSet set)
    {
        var names = new HashSet<string>(StringComparer.Ordinal)
        {
            SpecSetIndex.FileName,
            FileName(key.AccountingPath),
        };
        foreach (var phase in set.Phases)
        {
            names.Add(FileName(key.YamlPath(phase.FileStem)));
            names.Add(FileName(key.MarkdownPath(phase.FileStem)));
        }
        return names;
    }

    // The sandbox lists entries as absolute or repo-relative paths depending on the
    // reader — the spec directory is flat, so the file NAME is the stable identity.
    private static string FileName(string path) => path.TrimEnd('/').Split('/', '\\')[^1];

    private static bool IsSpecFile(string name) =>
        name.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase)
        || name.EndsWith(".md", StringComparison.OrdinalIgnoreCase);
}
