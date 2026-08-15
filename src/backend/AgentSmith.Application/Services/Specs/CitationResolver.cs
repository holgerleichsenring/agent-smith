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
public sealed class CitationResolver(DiffFileIndex diff, IReadOnlyList<string> commands)
{
    public CriterionAccount Resolve(AccountRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (!row.Satisfied)
            return new CriterionAccount(row.Criterion, false, null, row.Note);

        if (diff.Contains(row.Citation))
            return new CriterionAccount(row.Criterion, true, row.Citation, row.Note);

        if (commands.Any(command => Mentions(command, row.Citation)))
            return new CriterionAccount(row.Criterion, true, row.Citation, row.Note, Mechanical: true);

        return new CriterionAccount(
            row.Criterion, false, null,
            $"claimed satisfied by '{row.Citation ?? "(nothing cited)"}', which is neither "
            + "in the diff nor a command that ran");
    }

    private static bool Mentions(string commandResult, string? citation) =>
        !string.IsNullOrWhiteSpace(citation)
        && commandResult.Contains(citation.Trim(), StringComparison.OrdinalIgnoreCase);
}
