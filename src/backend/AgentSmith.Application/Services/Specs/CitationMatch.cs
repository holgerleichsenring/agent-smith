namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0473: whether a citation NAMES a command that ran. Split from
/// <see cref="CitationResolver"/>, which decides what an account row is worth: reading a
/// citation against one evidence line and judging a row against every file and command
/// are two reasons to change, and the resolver was at the file-length ceiling.
/// </summary>
internal static class CitationMatch
{
    /// <summary>How much of the command a citation has to name before it counts as naming
    /// it. A whole command is the normal case; this leaves room for a trailing argument the
    /// citation dropped, and refuses "dotnet" for "dotnet test".</summary>
    private const int MinPrefixPercent = 60;

    /// <summary>
    /// p0469: the citation must name the COMMAND, not something the command printed.
    /// <para>
    /// This matched a citation against the whole evidence line, output tail included, in
    /// both directions. That was tolerable while the account saw two to four build lines;
    /// with the agent's own commands in the evidence it sees up to forty, each carrying
    /// four hundred characters of output, and "contains" degrades toward always-true — a
    /// model could close a criterion by quoting a string it read in some command's output.
    /// </para>
    /// <para>
    /// Evidence is written in two grammars. A run names its command in quotes — "repo:
    /// build 'dotnet build' exited 0", "the agent ran 'grep -rn Legacy src' exited 1 —
    /// output: …" — and is cited by that command, which must be named substantially rather
    /// than merely occur somewhere in the line. A pipeline STEP names itself first —
    /// "DependencyAuditCommand: 0 advisories" — and is cited by that name, exactly.
    /// Neither reading can reach the output.
    /// </para>
    /// </summary>
    public static bool Names(string commandResult, string citation) =>
        NamesTheCommand(commandResult, citation) || NamesTheStep(commandResult, citation);

    private static bool NamesTheCommand(string commandResult, string citation)
    {
        var ran = EvidenceCommand.InEvidence(commandResult);
        if (ran.Length == 0) return false;
        // p0473: two readings of the citation, because both forms are honest. A verbatim
        // copy keeps the command's own apostrophes; a citation written the way the list
        // prints it puts the command inside quotes. Neither reading reaches the output.
        return NamesIt(ran, EvidenceCommand.InCitation(citation))
            || NamesIt(ran, EvidenceCommand.Quoted(citation));
    }

    /// <summary>
    /// p0473: the CITED text must be a leading part of the command that RAN, and never the
    /// other way round. The symmetric form let a citation that merely BEGINS with a real
    /// command resolve — "dotnet build' exited 0; worker: build 'never ran" is what a
    /// two-part citation yields when its quoted span is read across both parts, and it
    /// passed while naming a command that never ran. One direction, and the threshold keeps
    /// "dotnet" from standing in for "dotnet test".
    /// </summary>
    private static bool NamesIt(string ran, string cited)
    {
        if (cited.Length == 0) return false;
        return ran.StartsWith(cited, StringComparison.OrdinalIgnoreCase)
            && cited.Length * 100 >= ran.Length * MinPrefixPercent;
    }

    /// <summary>A step is cited by its name and nothing else — its message is output like
    /// any other, and a message quoting a skill name is not a citation of that skill.
    /// </summary>
    private static bool NamesTheStep(string commandResult, string citation)
    {
        var colon = commandResult.IndexOf(':');
        return colon > 0
            && string.Equals(commandResult[..colon].Trim(), citation.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The command a line names: the quoted span where there is one, the whole
    /// line where there is not. Never the output that follows it.</summary>
}
