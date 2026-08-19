using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0421: turns a CLAIM into a CHECKED claim.
/// <para>
/// Evidence comes in two halves and neither is optional. A criterion about the
/// repository's content is satisfied by a path the diff really touched; a criterion
/// about a build or test result is satisfied by a command that really ran — no diff
/// contains a build log, and demanding one made every build criterion outstanding over
/// builds that had gone green. A citation that names neither is a fabrication and the
/// criterion goes back to unsatisfied.
/// </para>
/// </summary>
public sealed class CitationResolver(CitedFileIndex files, IReadOnlyList<string> commands)
{
    /// <summary>How much of the command a citation has to name before it counts as naming
    /// it. A whole command is the normal case; this leaves room for a trailing argument the
    /// citation dropped, and refuses "dotnet" for "dotnet test".</summary>
    private const int MinPrefixPercent = 60;

    public CriterionAccount Resolve(AccountRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (!row.Satisfied)
            return new CriterionAccount(row.Criterion, false, null, row.Note);

        if (files.Contains(row.Citation))
            return new CriterionAccount(row.Criterion, true, row.Citation, row.Note);

        // A criterion about SEVERAL repositories is cited by several commands, joined in
        // one string — run 18 refused "Server: build exited 0; Worker: build exited 0"
        // because no single command contains the whole of it. Every part must resolve, so
        // citing one real command and one invented still fails.
        if (EveryPartRanAsACommand(row.Citation))
            return new CriterionAccount(row.Criterion, true, row.Citation, row.Note, Mechanical: true);

        return new CriterionAccount(
            row.Criterion, false, null,
            $"claimed satisfied by '{row.Citation ?? "(nothing cited)"}', which is neither "
            + "a file the evidence covers nor a command that ran");
    }

    private bool EveryPartRanAsACommand(string? citation)
    {
        if (string.IsNullOrWhiteSpace(citation)) return false;
        var parts = citation
            .Split([';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(part => part.Length > 0)
            .ToList();
        return parts.Count > 0 && parts.All(part => commands.Any(c => Mentions(c, part)));
    }

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
    private static bool Mentions(string commandResult, string citation) =>
        NamesTheCommand(commandResult, citation) || NamesTheStep(commandResult, citation);

    private static bool NamesTheCommand(string commandResult, string citation)
    {
        var ran = CommandOf(commandResult);
        var cited = CommandOf(citation);
        if (ran.Length == 0 || cited.Length == 0) return false;
        var (shorter, longer) = cited.Length <= ran.Length ? (cited, ran) : (ran, cited);
        return longer.StartsWith(shorter, StringComparison.OrdinalIgnoreCase)
            && shorter.Length * 100 >= ran.Length * MinPrefixPercent;
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
    private static string CommandOf(string text)
    {
        var open = text.IndexOf('\'');
        var close = text.LastIndexOf('\'');
        return (open >= 0 && close > open ? text[(open + 1)..close] : text).Trim();
    }
}
