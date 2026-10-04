namespace AgentSmith.Application.Services;

/// <summary>
/// 2026-10-04-2bf2: the operator-owned tail of a principles.md — its LAST
/// "## Project Specifics" section, through the end of the file. Everything above it is
/// catalog text a refresh regenerates; this section is the one part a refresh copies.
/// </summary>
internal static class ProjectSpecificsSection
{
    public const string Heading = "## Project Specifics";

    /// <summary>The section verbatim, or null when the file has no such heading — then a
    /// refresh has nothing to carry over.</summary>
    public static string? Extract(string? principles)
    {
        if (string.IsNullOrEmpty(principles)) return null;
        var start = LastHeadingStart(principles);
        return start < 0 ? null : principles[start..];
    }

    /// <summary>The composed file with its own (empty) Project Specifics section replaced by
    /// <paramref name="section"/>; a composition without one gets the section appended.</summary>
    public static string Append(string composed, string section)
    {
        var start = LastHeadingStart(composed);
        var head = start < 0 ? composed.TrimEnd() + "\n\n" : composed[..start];
        return head + section;
    }

    // A heading only at the start of a line: "### Project Specifics" is not this section.
    private static int LastHeadingStart(string text)
    {
        var searchFrom = text.Length - 1;
        while (searchFrom >= 0)
        {
            var index = text.LastIndexOf(Heading, searchFrom, StringComparison.Ordinal);
            if (index < 0) return -1;
            if (index == 0 || text[index - 1] == '\n') return index;
            searchFrom = index - 1;
        }
        return -1;
    }
}
