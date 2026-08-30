using System.Text;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;

namespace AgentSmith.Infrastructure.Services.Output;

/// <summary>
/// 2026-08-30-3c12: renders the settled requirement account into the delivered report.
/// <para>
/// The entry map says what the scan LOOKED AT; this says what it can show for it — per
/// group and per station, how many of the standard's entries came back met, unmet,
/// undecidable and unanswered, and which entry groups the run never reached at all. Empty
/// when nothing was answered, so a report from a run without it is the report it was before.
/// </para>
/// </summary>
public static class RequirementSection
{
    public const string Heading = "## Requirements — what each station answers";

    /// <summary>The section, or the empty string when this run answered nothing.</summary>
    public static string Markdown(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (!pipeline.TryGet<RequirementAccount>(ContextKeys.RequirementAccount, out var account)
            || account is null || account.IsEmpty)
            return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine(Heading).AppendLine();
        sb.AppendLine($"{account.AnsweredCount} of {account.Rows.Count} entries of "
            + $"{account.CatalogueVersion} answered across "
            + $"{account.Groups.Count(g => g.Attempted)} entry group(s).").AppendLine();
        foreach (var group in account.Groups) AppendGroup(sb, group);
        sb.AppendLine(account.Attribution).AppendLine();
        return sb.ToString();
    }

    private static void AppendGroup(StringBuilder sb, EntryGroupRequirements group)
    {
        sb.AppendLine($"### {group.Group}").AppendLine();
        if (!group.Attempted)
        {
            sb.AppendLine("NOT ATTEMPTED — beyond the entry groups this run answers for. "
                + "Nothing here was checked.").AppendLine();
            return;
        }

        sb.AppendLine("| Station | Operations | Met | Unmet | Cannot answer | Unanswered |");
        sb.AppendLine("| --- | --- | --- | --- | --- | --- |");
        foreach (var station in Enum.GetValues<VerificationStation>())
            foreach (var operation in Operations(group, station))
                AppendRow(sb, group, station, operation);
        sb.AppendLine();
        AppendNamedRows(sb, group);
    }

    /// <summary>Reads are always accounted for; writes appear once the group enumerated
    /// any, and a group that enumerated none says so rather than reading as complete.</summary>
    private static IEnumerable<RequirementOperation> Operations(
        EntryGroupRequirements group, VerificationStation station) =>
        Enum.GetValues<RequirementOperation>()
            .Where(o => o == RequirementOperation.Read
                || group.Rows.Any(r => r.Station == station && r.Operation == o));

    private static void AppendRow(
        StringBuilder sb, EntryGroupRequirements group, VerificationStation station,
        RequirementOperation operation)
    {
        var rows = group.Rows.Where(r => r.Station == station && r.Operation == operation).ToList();
        sb.AppendLine($"| {station} | {operation.ToString().ToLowerInvariant()} "
            + $"| {Count(rows, RequirementDisposition.Met)} "
            + $"| {Count(rows, RequirementDisposition.Unmet)} "
            + $"| {Count(rows, RequirementDisposition.CannotAnswer)} "
            + $"| {Count(rows, RequirementDisposition.Unanswered)} |");
    }

    /// <summary>The rows a reader must be able to look up: every entry the scan says is not
    /// met and every one it could not decide, each with its id and what it cited.</summary>
    private static void AppendNamedRows(StringBuilder sb, EntryGroupRequirements group)
    {
        if (!group.EnumeratesWrites)
            sb.AppendLine("No state-changing operation was enumerated for this group.")
                .AppendLine();
        foreach (var row in group.Unmet.Concat(group.Undecidable))
            sb.AppendLine($"- {row.Reference} {row.Station} "
                + $"({row.Operation.ToString().ToLowerInvariant()}) — "
                + $"{row.Disposition}: {Cell(row)}");
        sb.AppendLine();
    }

    private static int Count(IReadOnlyList<RequirementRow> rows, RequirementDisposition disposition) =>
        rows.Count(r => r.Disposition == disposition);

    private static string Cell(RequirementRow row) =>
        string.IsNullOrWhiteSpace(row.Citation) ? row.Note : $"`{row.Citation}`";
}
