using System.Text;
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

    private static string Listed(IReadOnlyList<DraftFile> files) =>
        files.Count == 0 ? "no file yet" : string.Join(", ", files.Select(f => DraftPath.Shown(f.Path)));
}
