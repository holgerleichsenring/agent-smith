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

        // A criterion about SEVERAL repositories is cited by several commands, joined in
        // one string — run 18 refused "Server: build exited 0; Worker: build exited 0"
        // because no single command contains the whole of it. Every part must resolve, so
        // citing one real command and one invented still fails.
        if (EveryPartRanAsACommand(row.Citation))
            return new CriterionAccount(row.Criterion, true, row.Citation, row.Note, Mechanical: true);

        return new CriterionAccount(
            row.Criterion, false, null,
            $"claimed satisfied by '{row.Citation ?? "(nothing cited)"}', which is neither "
            + "in the diff nor a command that ran");
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

    private static bool Mentions(string commandResult, string citation) =>
        commandResult.Contains(citation, StringComparison.OrdinalIgnoreCase)
        || citation.Contains(commandResult, StringComparison.OrdinalIgnoreCase);
}
