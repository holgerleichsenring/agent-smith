using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Scans;

/// <summary>
/// 2026-08-30-3c12: the rows of the account a reader must see among the findings — an entry
/// the scan says is NOT met, an entry it could not decide and the input it lacked, the same
/// entry met on the read path and failed on the write path, and an entry group the run
/// never reached.
/// <para>
/// They ship at INFO and never block, like the entry map they stand on. Three identical
/// runs of one repository once delivered 25, 26 and 37 findings, and the untriaged run
/// looked like the best of them — so a statement about what the SCAN covered stays out of
/// the tally a reader reads as vulnerabilities, and out of the ledger the delivery gate
/// reads. An entry's level is the standard's own and is never re-scored into a severity.
/// </para>
/// </summary>
public static class RequirementFindings
{
    public const string Role = "requirement-account";
    public const string Category = "verification";

    private const int MaxQuotedText = 200;

    public static IReadOnlyList<SkillObservation> For(RequirementAccount account)
    {
        ArgumentNullException.ThrowIfNull(account);
        return
        [
            .. account.Groups.Where(g => g.Attempted).SelectMany(g => Rows(g, account.Attribution)),
            .. account.NotAttempted.Select(g => NotAttempted(g.Group, account))
        ];
    }

    private static IEnumerable<SkillObservation> Rows(EntryGroupRequirements group, string attribution) =>
    [
        .. group.Unmet.Select(row => Unmet(group.Group, row, attribution)),
        .. group.Undecidable.Select(row => Undecidable(group.Group, row, attribution)),
        .. group.ReadWriteAsymmetries.Select(row => Asymmetric(group.Group, row, attribution))
    ];

    private static SkillObservation Unmet(string group, RequirementRow row, string attribution) =>
        Finding($"{row.Reference} not met — '{group}', {Name(row.Station)} station, "
                + $"{Name(row.Operation)} operations: {Quoted(row)}",
            $"Satisfy {row.RequirementId} at the {Name(row.Station)} station of '{group}', or "
                + $"record why this system does not need it. Cited: {Cited(row)}",
            $"The scan answered {row.Reference} of the verification standard at this station "
                + $"and states it is not satisfied. Cited: {Cited(row)}", attribution);

    private static SkillObservation Undecidable(string group, RequirementRow row, string attribution) =>
        Finding($"{row.Reference} undecidable — '{group}', {Name(row.Station)} station, "
                + $"{Name(row.Operation)} operations: {row.Note}",
            $"Give the scan the input it named as missing, then run it again: {row.Note}",
            $"The scan could not decide {row.Reference} here and named the input it would have "
                + "needed. That is a knowledge gap, not a passing verdict.", attribution);

    private static SkillObservation Asymmetric(string group, RequirementRow row, string attribution) =>
        Finding($"{row.Reference} holds on reads but not on writes — '{group}', "
                + $"{Name(row.Station)} station: {Quoted(row)}",
            $"Apply the same {Name(row.Station)} check to the state-changing operations of "
                + $"'{group}' that its reads already pass. Cited: {Cited(row)}",
            "The same entry is met on the read path and not met on the write path of one "
                + "group. A review that follows only the read path never reaches this row.",
            attribution);

    private static SkillObservation NotAttempted(string group, RequirementAccount account) =>
        Finding($"Entry group '{group}' was not attempted — this run answers at most "
                + $"{RequirementAnswerLog.MaxEntryGroups} entry group(s)",
            $"Scan '{group}' in a run of its own, or raise the number of entry groups one run "
                + "answers for.",
            $"No entry of {account.CatalogueVersion} was asked of this group. That is a budget "
                + "fact about the run, not a verdict about the code — nothing here was checked.",
            account.Attribution);

    private static SkillObservation Finding(
        string description, string suggestion, string rationale, string attribution) =>
        new(Id: 0, Role: Role,
            Concern: ObservationConcern.Security,
            Description: description,
            Suggestion: suggestion,
            Blocking: false,
            Severity: ObservationSeverity.Info,
            Confidence: 100,
            Rationale: $"{rationale} {attribution}".Trim(),
            EvidenceMode: EvidenceMode.Potential,
            Category: Category);

    private static string Quoted(RequirementRow row) =>
        row.Text.Length <= MaxQuotedText ? row.Text : row.Text[..MaxQuotedText] + "…";

    private static string Cited(RequirementRow row) =>
        string.IsNullOrWhiteSpace(row.Citation) ? "nothing" : row.Citation;

    private static string Name(VerificationStation station) => station.ToString().ToLowerInvariant();

    private static string Name(RequirementOperation operation) => operation.ToString().ToLowerInvariant();
}
