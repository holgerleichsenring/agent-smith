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
/// <para>
/// 2026-08-25-9749: a third claim, NOT APPLICABLE, is checked against a third half —
/// searches of the BASE that ran and found nothing. <see cref="NotApplicableAdmission"/>
/// holds that rule; what a row is worth and what admits one disposition are two reasons to
/// change.
/// </para>
/// </summary>
public sealed class CitationResolver(
    CitedFileIndex files,
    IReadOnlyList<string> commands,
    IReadOnlyList<string>? baseAbsences = null)
{
    private readonly NotApplicableAdmission _admission = new(baseAbsences ?? []);

    public CriterionAccount Resolve(AccountRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return row.Disposition switch
        {
            AccountDisposition.Satisfied => Satisfied(row),
            AccountDisposition.NotApplicable => NotApplicable(row),
            _ => new CriterionAccount(row.Criterion, AccountDisposition.NotSatisfied, null, row.Note),
        };
    }

    private CriterionAccount Satisfied(AccountRow row)
    {
        var cited = row.Cited;
        if (cited.Count == 0)
            return Refused(row, "claimed satisfied but cited nothing");

        // p0474: every element stands on its own — one diff path, or one command copied
        // whole. Nothing is split: a separator that could join two citations also occurs
        // inside the commands they name, which is what made a correctly quoted command
        // unresolvable.
        var unresolved = cited.FirstOrDefault(c => !files.Contains(c) && !RanAsACommand(c));
        if (unresolved is not null)
            return Refused(row,
                $"claimed satisfied by '{unresolved}', which is neither a file the evidence "
                + "covers nor a command that ran");

        return new CriterionAccount(
            row.Criterion, AccountDisposition.Satisfied, string.Join("; ", cited), row.Note,
            Mechanical: cited.All(c => !files.Contains(c)));
    }

    /// <summary>A claim the admission rule refuses degrades to NOT SATISFIED and says why —
    /// it is never an error, because the fallback is the answer the row would have had
    /// before the third disposition existed.</summary>
    private CriterionAccount NotApplicable(AccountRow row)
    {
        var refusal = _admission.Refusal(row);
        return refusal is not null
            ? Refused(row, refusal)
            : new CriterionAccount(
                row.Criterion, AccountDisposition.NotApplicable, _admission.Proof(row), row.Note,
                Antecedent: row.Antecedent);
    }

    private static CriterionAccount Refused(AccountRow row, string note) =>
        new(row.Criterion, AccountDisposition.NotSatisfied, null, note);

    private bool RanAsACommand(string citation) =>
        commands.Any(c => CitationMatch.Names(c, citation));
}
