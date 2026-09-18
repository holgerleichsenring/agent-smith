using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-17-042eh: a unified diff, split per file and cut to a bound WHOLE FILES FIRST.
/// <para>
/// A prompt bound taken with a character cut would hand the reviewer the last file
/// half-shown, and a finding on the half it could not see is indistinguishable from one on
/// the half it could. So the bound is taken at a file boundary and everything past it is
/// LISTED by name: a reviewer told which files it was not shown states nothing about them,
/// and the admission discards a finding on one of them anyway.
/// </para>
/// </summary>
public static class PhaseDiffText
{
    /// <summary>What one prompt may carry of one sandbox's diff.</summary>
    public const int MaxChars = 120_000;

    /// <summary>
    /// The diff's files, in the order git emitted them, minus what a review cannot be about:
    /// the run's own record under <c>.agentsmith/runs/</c>, and a hunk git reports as binary.
    /// <para>
    /// The NARROW record check (p0322c), not the broad one: an init or bootstrap phase whose
    /// deliverable IS <c>.agentsmith/contexts/*/principles.md</c> would otherwise be shown as
    /// a phase that changed nothing, and every finding on its real work discarded. A binary
    /// hunk carries no line to cite, so no finding on it could ever be admitted — showing it
    /// spends the bound on text nobody can act on.
    /// </para>
    /// </summary>
    public static IReadOnlyList<PhaseDiffFile> Files(string? diff)
    {
        var text = diff ?? string.Empty;
        var files = new List<PhaseDiffFile>();
        var starts = Boundaries(text);
        for (var i = 0; i < starts.Count; i++)
        {
            var end = i + 1 < starts.Count ? starts[i + 1] : text.Length;
            var chunk = text[starts[i]..end];
            var path = PhaseDiffPath.Of(chunk);
            if (path is null || RunRecordPaths.IsUnderRunsDir(path) || IsBinary(chunk)) continue;
            files.Add(new PhaseDiffFile(path, chunk));
        }
        return files;
    }

    /// <summary>
    /// Packs whole files up to the bound; the rest are named as unreviewed. A file that does
    /// not fit is SKIPPED, not terminal — one oversized generated artefact early in the diff
    /// must not blank every ordinary source file behind it.
    /// </summary>
    public static PhaseDiffBound Bound(IReadOnlyList<PhaseDiffFile> files, int maxChars)
    {
        ArgumentNullException.ThrowIfNull(files);
        var shown = new List<PhaseDiffFile>();
        var unreviewed = new List<string>();
        var used = 0;
        foreach (var file in files)
        {
            // A single file over the remaining bound is unreviewable, not truncatable: shown
            // in part it would invite a finding on the part that was cut.
            if (used + file.Text.Length > maxChars)
            {
                unreviewed.Add(file.Path);
                continue;
            }
            shown.Add(file);
            used += file.Text.Length;
        }
        return new PhaseDiffBound(
            string.Concat(shown.Select(f => f.Text)),
            [.. shown.Select(f => f.Path)],
            unreviewed);
    }

    private static bool IsBinary(string chunk) =>
        chunk.Contains("\nGIT binary patch", StringComparison.Ordinal)
        || chunk.Contains("\nBinary files ", StringComparison.Ordinal);

    private static IReadOnlyList<int> Boundaries(string text)
    {
        const string marker = "diff --git ";
        var starts = new List<int>();
        var at = text.StartsWith(marker, StringComparison.Ordinal)
            ? 0
            : text.IndexOf("\n" + marker, StringComparison.Ordinal) is var first && first >= 0 ? first + 1 : -1;
        while (at >= 0)
        {
            starts.Add(at);
            var next = text.IndexOf("\n" + marker, at + 1, StringComparison.Ordinal);
            at = next < 0 ? -1 : next + 1;
        }
        return starts;
    }
}

/// <summary>One file's hunk of a diff, under the path it ends up at.</summary>
public sealed record PhaseDiffFile(string Path, string Text);

/// <summary>What fit inside the bound, and what a finding may therefore rest on.</summary>
/// <param name="Unreviewed">Files past the bound — named to the reviewer, and a finding on
/// one of them is not kept.</param>
public sealed record PhaseDiffBound(
    string Text, IReadOnlyList<string> Paths, IReadOnlyList<string> Unreviewed);
