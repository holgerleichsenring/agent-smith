namespace AgentSmith.Infrastructure.Services.Security;

/// <summary>
/// Extracts CVE identifiers from URLs or text.
/// </summary>
internal static class CveExtractor
{
    internal static string? Extract(string text)
    {
        var idx = text.IndexOf("CVE-", StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
            return null;

        var end = idx + 4;
        while (end < text.Length && (char.IsDigit(text[end]) || text[end] == '-'))
            end++;

        var cve = text[idx..end];
        return cve.Length > 4 ? cve : null;
    }
}
