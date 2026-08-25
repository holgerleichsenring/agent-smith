namespace AgentSmith.Tests.Architecture;

/// <summary>
/// 2026-08-25-5266: the context files that LEAVE this repository — the methodology
/// template every new project starts from, this repository's own contexts, and the
/// demo sample packed into the product binary.
/// <para>
/// Discovered, never listed: a context added under a shipped root is judged the
/// moment it exists, which is the only form of coverage that survives a third file
/// being added by somebody who never read this rule.
/// </para>
/// <para>
/// <c>tests/</c> is not a shipped root. Its context files are INPUTS handed to the
/// product — several are deliberately malformed, and the harness data fixtures exist
/// precisely to be refused. Judging them would assert the opposite of their purpose.
/// </para>
/// </summary>
internal static class ShippedContextFiles
{
    public static IReadOnlyList<string> All { get; } =
    [
        Path.GetFullPath(ContextSchemaFile.TemplatePath),
        .. Under(ArchitectureSources.AgentSmithRoot),
        .. Under(ArchitectureSources.SourceRoot)
    ];

    public static TheoryData<string> AsTheoryData()
    {
        var data = new TheoryData<string>();
        foreach (var path in All) data.Add(path);
        return data;
    }

    private static IEnumerable<string> Under(string root) =>
        Directory.EnumerateFiles(root, "context.yaml", SearchOption.AllDirectories)
            .Where(IsAContextFile)
            .Where(path => !IsBuildOutput(path))
            .Select(Path.GetFullPath)
            .OrderBy(path => path, StringComparer.Ordinal);

    // A context lives at <anything>/.agentsmith/contexts/<name>/context.yaml. Matching
    // the whole shape rather than the file name keeps an unrelated context.yaml — a
    // fixture payload, a doc example — from being judged as one.
    private static bool IsAContextFile(string path)
    {
        var contexts = Path.GetDirectoryName(Path.GetDirectoryName(path));
        return Path.GetFileName(contexts) == "contexts"
            && Path.GetFileName(Path.GetDirectoryName(contexts)) == ".agentsmith";
    }

    // Build output is a copy, not a source: the demo sample is packed per build, so
    // its bin/obj duplicates would be judged twice and could lag the checked-in file.
    private static bool IsBuildOutput(string path) =>
        path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        || path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
}
