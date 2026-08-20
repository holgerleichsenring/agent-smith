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
    public CriterionAccount Resolve(AccountRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (!row.Satisfied)
            return new CriterionAccount(row.Criterion, false, null, row.Note);

        var cited = row.Cited;
        if (cited.Count == 0)
            return new CriterionAccount(
                row.Criterion, false, null, "claimed satisfied but cited nothing");

        // p0474: every element stands on its own — one diff path, or one command copied
        // whole. Nothing is split: a separator that could join two citations also occurs
        // inside the commands they name, which is what made a correctly quoted command
        // unresolvable.
        var unresolved = cited.FirstOrDefault(c => !files.Contains(c) && !RanAsACommand(c));
        if (unresolved is not null)
            return new CriterionAccount(
                row.Criterion, false, null,
                $"claimed satisfied by '{unresolved}', which is neither a file the evidence "
                + "covers nor a command that ran");

        var citation = string.Join("; ", cited);
        return new CriterionAccount(
            row.Criterion, true, citation, row.Note,
            Mechanical: cited.All(c => !files.Contains(c)));
    }

    private bool RanAsACommand(string citation) =>
        commands.Any(c => CitationMatch.Names(c, citation));

}
