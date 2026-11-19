using System.Text;
using System.Text.RegularExpressions;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Application.Services.SpecDialog.Writes;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-11-15-06fc: read_file and directory_tree on a <c>draft:</c> path — the design turn's draft files, numbered
/// the way a checkout's file is read, and listed by their draft: paths.
/// </summary>
internal static class DraftFileReads
{
    public static string Read(IReadOnlyList<DraftFile> files, string path, int? startLine, int? lineCount, bool numbered)
    {
        var bare = DraftPath.Bare(path);
        if (files.FirstOrDefault(f => f.Path == bare) is not { } file)
            return $"Error: {DraftPath.Shown(bare)} is no file of your draft. It holds: {Listed(files)}.";
        var text = file.Text.EndsWith('\n') ? file.Text[..^1] : file.Text;
        var lines = text.Split('\n');
        var start = Math.Clamp(startLine ?? 1, 1, lines.Length + 1);
        var end = (int)Math.Min((long)start - 1 + (lineCount ?? int.MaxValue), lines.Length);
        var width = lines.Length.ToString().Length;
        var sb = new StringBuilder();
        for (var i = start - 1; i < end; i++)
            sb.Append(numbered ? (i + 1).ToString().PadLeft(width) + "\t" : string.Empty).Append(lines[i]).Append('\n');
        return sb.ToString().TrimEnd('\n');
    }

    public static string Tree(IReadOnlyList<DraftFile> files, string root)
    {
        var under = DraftPath.Bare(root).TrimEnd('/');
        var listed = files.Where(f => under.Length == 0 || f.Path.StartsWith(under + "/", StringComparison.Ordinal)).ToList();
        return listed.Count == 0
            ? $"{DraftPath.Shown(under)} holds no file yet."
            : string.Join('\n', listed.Select(f => DraftPath.Shown(f.Path)));
    }

    /// <summary>2026-11-19-b2c1: find_files under a draft: root — the draft paths whose path below the root matches the
    /// glob (or contains the pattern when it has no glob characters).</summary>
    public static string Find(IReadOnlyList<DraftFile> files, string root, string pattern)
    {
        var matched = Under(files, root).Where(f => Matches(f.Path, pattern)).Select(f => DraftPath.Shown(f.Path)).ToList();
        return matched.Count == 0 ? $"No file of the draft under {DraftPath.Shown(DraftPath.Bare(root))} matches '{pattern}'." : string.Join('\n', matched);
    }

    /// <summary>2026-11-19-b2c1: grep_in_tree under a draft: root — each matching line as path:line:text, the files
    /// with a match, or the count per file.</summary>
    public static string Grep(IReadOnlyList<DraftFile> files, string root, string pattern, string? glob, GrepOutputMode mode)
    {
        Regex regex;
        try { regex = new Regex(pattern, RegexOptions.NonBacktracking); }
        catch (ArgumentException ex) { return $"Error: '{pattern}' is not a valid regular expression ({ex.Message})."; }
        var sb = new StringBuilder();
        foreach (var file in Under(files, root).Where(f => glob is null || Matches(f.Path, glob)))
        {
            var hits = file.Text.Split('\n').Select((line, i) => (line, n: i + 1)).Where(l => regex.IsMatch(l.line)).ToList();
            if (hits.Count == 0) continue;
            var shown = DraftPath.Shown(file.Path);
            if (mode == GrepOutputMode.FilesWithMatches) sb.Append(shown).Append('\n');
            else if (mode == GrepOutputMode.Count) sb.Append(shown).Append(':').Append(hits.Count).Append('\n');
            else foreach (var (line, n) in hits) sb.Append(shown).Append(':').Append(n).Append(':').Append(line).Append('\n');
        }
        return sb.Length == 0 ? $"No line of the draft under {DraftPath.Shown(DraftPath.Bare(root))} matches '{pattern}'." : sb.ToString().TrimEnd('\n');
    }

    private static IEnumerable<DraftFile> Under(IReadOnlyList<DraftFile> files, string root)
    {
        var under = DraftPath.Bare(root).TrimEnd('/');
        return files.Where(f => under.Length == 0 || f.Path == under || f.Path.StartsWith(under + "/", StringComparison.Ordinal));
    }

    private static bool Matches(string path, string pattern)
    {
        if (pattern.IndexOfAny(['*', '?']) < 0) return path.Contains(pattern, StringComparison.Ordinal);
        var regex = "^" + Regex.Escape(pattern).Replace(@"\*\*/", "(.*/)?").Replace(@"\*\*", ".*").Replace(@"\*", "[^/]*").Replace(@"\?", "[^/]") + "$";
        var name = path[(path.LastIndexOf('/') + 1)..];
        return Regex.IsMatch(path, regex) || Regex.IsMatch(name, regex);
    }

    private static string Listed(IReadOnlyList<DraftFile> files) =>
        files.Count == 0 ? "no file yet" : string.Join(", ", files.Select(f => DraftPath.Shown(f.Path)));
}
