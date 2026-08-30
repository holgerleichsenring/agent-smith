using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Scans;

/// <summary>
/// 2026-08-30-03e1: the rows of the account a reader must see among the findings — a
/// finding that names the entry of the standard it breaks and cites a place this scan
/// really read, and an entry group the run never reached.
/// <para>
/// A citation that resolved against nothing is NOT here. It is silence dressed as evidence
/// and it stays in the report, where a reader can see the scan claimed something it could
/// not show. A finding no entry covers is not here either, and never was: it travels the
/// ordinary observation path and is delivered unchanged.
/// </para>
/// <para>
/// These ship at INFO and never block, like the entry map they stand on. Three identical
/// runs of one repository once delivered 25, 26 and 37 findings, and the untriaged run
/// looked like the best of them — so a statement about what the SCAN covered stays out of
/// the tally a reader reads as vulnerabilities, and out of the ledger the delivery gate
/// reads. An entry's level is the standard's own and is never re-scored into a severity.
/// </para>
/// </summary>
public static class CitedFindingObservations
{
    public const string Role = "requirement-citation";
    public const string Category = "verification";

    private const int MaxQuotedText = 200;

    public static IReadOnlyList<SkillObservation> For(ScanExaminationAccount account)
    {
        ArgumentNullException.ThrowIfNull(account);
        return
        [
            .. account.Located.Select(cited => Cited(cited.Group, cited.Row, account.Attribution)),
            .. account.NotAttempted.Select(group => NotAttempted(group.Group, account))
        ];
    }

    private static SkillObservation Cited(string group, CitedFindingRow row, string attribution) =>
        Finding($"{row.Reference} broken — '{group}', {Name(row.Station)} station: {row.Detail}",
            $"Satisfy {row.RequirementId} at the {Name(row.Station)} station of '{group}', or "
                + $"record why this system does not need it. Cited: {row.Citation}",
            $"The scan states this breaks {row.Reference} of the verification standard, and "
                + $"cites {row.Citation}, a place it read this run. The entry reads: "
                + $"{Quoted(row)}", attribution);

    private static SkillObservation NotAttempted(string group, ScanExaminationAccount account) =>
        Finding($"Entry group '{group}' was not attempted — this run accounts for at most "
                + $"{CitedFindingLog.MaxEntryGroups} entry group(s)",
            $"Scan '{group}' in a run of its own, or raise the number of entry groups one run "
                + "accounts for.",
            $"No station of this group was examined against {account.CatalogueVersion}. That is "
                + "a budget fact about the run, not a verdict about the code — nothing here was "
                + "checked.", account.Attribution);

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

    private static string Quoted(CitedFindingRow row) =>
        row.Text.Length <= MaxQuotedText ? row.Text : row.Text[..MaxQuotedText] + "…";

    private static string Name(VerificationStation station) => station.ToString().ToLowerInvariant();
}
