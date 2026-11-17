namespace AgentSmith.PipelineHarness.Composition;

/// <summary>
/// 2026-11-17-5701b: every harness composition extracts the skills catalog into its own cache directory. The
/// fixtures share one <c>skills.cache_dir</c>, and since filing runs as a pipeline that resolves the catalog, two
/// compositions composing in parallel extracted into the same directory and broke each other ("Directory not
/// empty", a missing part of the path). The fixture is copied with that one value replaced; a fixture naming no
/// cache directory is used as it is.
/// </summary>
internal static class HarnessSkillsCache
{
    private const string Key = "cache_dir:";

    /// <summary>The fixture's path, or a temp copy whose cache_dir is a directory of its own.</summary>
    public static string Isolate(string configPath)
    {
        var lines = File.ReadAllLines(configPath);
        var at = Array.FindIndex(lines, l => l.TrimStart().StartsWith(Key, StringComparison.Ordinal));
        if (at < 0) return configPath;
        var id = Guid.NewGuid().ToString("N");
        var indent = lines[at][..(lines[at].Length - lines[at].TrimStart().Length)];
        lines[at] = $"{indent}{Key} {Path.Combine(Path.GetTempPath(), $"agentsmith-harness-skills-{id}")}";
        // Beside the fixture, so a path resolved against the config's directory still finds what the fixture names.
        var copy = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(configPath))!, $".harness-{id}-{Path.GetFileName(configPath)}");
        File.WriteAllLines(copy, lines);
        return copy;
    }
}
