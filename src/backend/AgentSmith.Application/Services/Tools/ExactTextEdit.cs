using System.Text;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// The exact-replacement rule of the edit tools: <c>old_string</c> must appear exactly once, or every occurrence is
/// replaced with <c>replace_all</c> and counted. 2026-11-15-06fc: one rule for the coding master's edit on a checkout
/// and the design master's edit_file on its draft. The reason a replacement cannot be made carries no "Error:" head.
/// </summary>
internal static class ExactTextEdit
{
    public static (string Content, int Count, string? Error) Apply(
        string content, string oldString, string newString, bool replaceAll, string path)
    {
        var idx = content.IndexOf(oldString, StringComparison.Ordinal);
        if (idx < 0) return (content, 0, $"old_string not found in {path}.");
        if (!replaceAll && content.IndexOf(oldString, idx + 1, StringComparison.Ordinal) >= 0)
            return (content, 0, $"old_string appears multiple times in {path} — extend the snippet with surrounding context to make it unique, or pass replace_all=true.");
        if (!replaceAll)
            return (string.Concat(content.AsSpan(0, idx), newString, content.AsSpan(idx + oldString.Length)), 1, null);
        // Manual replace_all so we can count and avoid Regex pitfalls with special chars.
        var sb = new StringBuilder(content.Length);
        var pos = 0;
        var count = 0;
        while (pos < content.Length)
        {
            var next = content.IndexOf(oldString, pos, StringComparison.Ordinal);
            if (next < 0) { sb.Append(content, pos, content.Length - pos); break; }
            sb.Append(content, pos, next - pos).Append(newString);
            pos = next + oldString.Length;
            count++;
        }
        return (sb.ToString(), count, null);
    }
}
