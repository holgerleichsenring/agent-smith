namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0473: reads the command out of an evidence line, and out of a citation of one.
/// <para>
/// Both grammars wrap the command in apostrophes and follow it with an exit clause —
/// "repo: build 'dotnet build' exited 0", "the agent ran 'grep -rn Sample src' exited 1".
/// Taking the FIRST and LAST apostrophe therefore works only while the command carries
/// none of its own, and a heredoc does: <c>python3 - &lt;&lt;'PY' … PY</c> made the slice
/// begin inside the marker, so even a verbatim citation of that command could not resolve.
/// The closing delimiter is the apostrophe that introduces the exit clause.
/// </para>
/// <para>
/// Separated from <see cref="CitationResolver"/> because reading the line's grammar and
/// deciding whether a citation resolves are two reasons to change, and the resolver is
/// close to the file-length ceiling.
/// </para>
/// </summary>
internal static class EvidenceCommand
{
    private const string ExitClause = "' exit";

    /// <summary>The command an evidence line says ran.</summary>
    public static string InEvidence(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        var open = line.IndexOf('\'');
        if (open < 0) return line.Trim();
        var close = line.LastIndexOf(ExitClause, StringComparison.Ordinal);
        return close > open
            ? line[(open + 1)..close].Trim()
            : line[(open + 1)..].Trim();
    }

    /// <summary>The citation as written. The account is told to copy the command verbatim,
    /// and a verbatim copy of a heredoc carries apostrophes of its own — so this reading
    /// keeps them and lets the caller compare the whole thing.</summary>
    public static string InCitation(string citation)
    {
        ArgumentNullException.ThrowIfNull(citation);
        return citation.Trim();
    }

    /// <summary>The citation's own quoted span, for the account that cites a command the way
    /// the list prints it — "build 'dotnet build'", or a whole evidence line copied across.
    /// Empty when the citation quotes nothing, so the caller falls back to the plain reading.
    /// </summary>
    public static string Quoted(string citation)
    {
        ArgumentNullException.ThrowIfNull(citation);
        var open = citation.IndexOf('\'');
        var close = citation.LastIndexOf('\'');
        return open >= 0 && close > open ? citation[(open + 1)..close].Trim() : string.Empty;
    }
}
