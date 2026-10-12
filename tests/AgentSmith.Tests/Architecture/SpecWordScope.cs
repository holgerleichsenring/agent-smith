using System.Diagnostics;

namespace AgentSmith.Tests.Architecture;

/// <summary>
/// 2026-10-12-967ag: the files the spec-word guard reads — what this repository writes from
/// here on, as git sees it: tracked and untracked, never ignored. Asking git keeps build
/// output, generated files and an operator's local configuration out without copying
/// .gitignore into a test, and a file written a minute ago is read before it is staged.
/// <para>
/// History is out by path: the spec, decision and note records, the context record
/// (<c>context.yaml</c> is the index of what shipped), run records and logs, changelogs and
/// EF migrations. They say phase because that was the word on the day.
/// </para>
/// </summary>
internal static class SpecWordScope
{
    private static readonly string[] Included =
    [
        "src/", "tests/", "docs/", "website/src/", "config/", "deploy/", "tools/", "hooks/",
        ".github/", ".claude/", ".agentsmith/contexts/", ".agentsmith/memory/",
        ".agentsmith/schemas/", ".agentsmith/security/", ".agentsmith/PARKED.md",
        ".agentsmith/code-map.yaml", ".agentsmith/skill.yaml", ".agentsmith/template.context.yaml",
        ".agentsmith/context.schema.json", ".agentsmith/decision.schema.json",
        ".agentsmith/spec.schema.json", "CLAUDE.md", "README.md", "WORKER-MODE.md", "DESIGN.md",
    ];

    /// <summary>The guard's own sources and data name every token they judge.</summary>
    private static readonly string[] Excluded =
    [
        "docs/reference/run-logs/", "tests/AgentSmith.Tests/Architecture/SpecWord",
        "tests/AgentSmith.Tests/Architecture/spec-word-",
    ];

    public static IReadOnlyList<SpecWordFile> Files() =>
    [
        .. GitListedPaths()
            .Where(InScope)
            .Select(path => (Path: path, Full: Path.Combine(ArchitectureSources.RepositoryRoot, path)))
            .Where(file => File.Exists(file.Full))
            .Select(file => (file.Path, Text: File.ReadAllText(file.Full)))
            .Where(file => !file.Text.Contains('\0'))
            .Select(file => SpecWordFile.Of(file.Path, file.Text)),
    ];

    internal static bool InScope(string path) =>
        Included.Any(prefix => path.StartsWith(prefix, StringComparison.Ordinal))
        && !Excluded.Any(prefix => path.StartsWith(prefix, StringComparison.Ordinal))
        && !path.Contains("/Migrations/", StringComparison.Ordinal)
        && !Path.GetFileName(path).StartsWith("CHANGELOG", StringComparison.Ordinal)
        && !(path.StartsWith(".agentsmith/contexts/", StringComparison.Ordinal)
             && path.EndsWith("/context.yaml", StringComparison.Ordinal));

    private static IEnumerable<string> GitListedPaths()
    {
        var psi = new ProcessStartInfo("git", "ls-files -z --cached --others --exclude-standard")
        {
            WorkingDirectory = ArchitectureSources.RepositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        using var git = Process.Start(psi)
            ?? throw new InvalidOperationException("git could not be started");
        var listed = git.StandardOutput.ReadToEnd();
        git.WaitForExit();
        if (git.ExitCode != 0)
            throw new InvalidOperationException($"git ls-files failed: {git.StandardError.ReadToEnd()}");
        return listed.Split('\0', StringSplitOptions.RemoveEmptyEntries);
    }
}
