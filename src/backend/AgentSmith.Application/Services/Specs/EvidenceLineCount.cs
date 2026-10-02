namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-02-3f06b: the one rule for how many lines a cited file has, shared by every probe so
/// a line range means the same against a working tree and against a sandbox read. Lines end at
/// '\n'; a trailing newline closes the last line rather than opening an empty one; an empty
/// file has none. A '\r' is content, so CRLF files count as their editors show them.
/// </summary>
public static class EvidenceLineCount
{
    public static int Of(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (content.Length == 0) return 0;
        var newlines = content.Count(c => c == '\n');
        return content[^1] == '\n' ? newlines : newlines + 1;
    }
}
