using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-09-22-46ef: the file search as a process the SERVER builds — its operands, and what
/// its answer means.
/// <para>
/// It used to be a shell string with a pipe into a head, which a read-only source scope
/// refused, and its root came straight from the model. find reads a leading dash as the start
/// of its EXPRESSION, so a root of <c>-delete</c> would arrive as a deletion primary over the
/// whole checkout — with no shell anywhere. So the root is rooted under the work path here,
/// the pattern is safe where it already is (an operand of <c>-path</c> cannot be read as an
/// option), and the head limit is applied to the RESULT rather than by a pipe.
/// </para>
/// </summary>
internal static class FileSearchStep
{
    public const string Program = "find";

    /// <summary>The arguments for a root already contained under the work path.</summary>
    public static IReadOnlyList<string> Arguments(string relativeRoot, string pattern) =>
        [Specs.ContainedPath.Absolute(relativeRoot), "-type", "f", "-path", NormalizePattern(pattern)];

    private static string NormalizePattern(string pattern)
    {
        var normalized = pattern.Replace("**", "*");
        var hasGlob = normalized.Contains('*') || normalized.Contains('?');
        return hasGlob ? normalized : $"*{normalized}*";
    }

    /// <summary>
    /// The matches, bounded, as repo-relative paths — or a refusal. An empty result from a
    /// program that never ran is a fabricated absence, and this surface exists to ground
    /// claims; a bad exit WITH matches is still matches, because find reports an unreadable
    /// directory that way.
    /// </summary>
    public static string Format(ProgramRun run, int limit)
    {
        var lines = ExtractStdoutSection(run.Rendered)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(RepoRelative)
            .ToList();
        if (run.ExitCode != 0 && lines.Count == 0)
            return $"Error: {Program} did not run in this sandbox, so nothing was searched — "
                   + $"this is not an absence.\n{run.Rendered}";
        if (lines.Count <= limit) return string.Join('\n', lines);
        return string.Join('\n', lines.Take(limit)) + $"\n(truncated: {limit} matches)";
    }

    /// <summary>The path as the model addresses files, now that find is given a rooted one.</summary>
    private static string RepoRelative(string line) =>
        line.StartsWith(Repository.SandboxWorkPath + "/", StringComparison.Ordinal)
            ? line[(Repository.SandboxWorkPath.Length + 1)..]
            : line;

    /// <summary>RunProgramAsync renders the labeled-section format; this pulls stdout out.</summary>
    private static string ExtractStdoutSection(string structured)
    {
        var idx = structured.IndexOf("stdout:\n", StringComparison.Ordinal);
        if (idx < 0) return string.Empty;
        var start = idx + "stdout:\n".Length;
        var end = structured.IndexOf("\n\nstderr:", start, StringComparison.Ordinal);
        return end < 0 ? structured[start..] : structured[start..end];
    }
}
