using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Scans;

/// <summary>
/// 2026-08-30-3c12: turns what the scan SAID about the standard's entries into what the run
/// can SHOW about them, group by group and station by station.
/// <para>
/// The denominator is external: the lens table says which entries a station is asked and the
/// entry map says which groups exist, so the count does not move with what the model
/// remembered to answer. The read axis is the denominator every attempted group is held to;
/// state-changing operations are enumerated on top of it, apart, because a verdict averaged
/// over both is the one that hides the asymmetry worth finding.
/// </para>
/// </summary>
public sealed class RequirementAccountant(IVerificationLens lens)
{
    private const RequirementOperation Read = RequirementOperation.Read;
    private const RequirementOperation Write = RequirementOperation.Write;

    public RequirementAccount Settle(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        var answers = RequirementAnswerLog.In(pipeline);
        if (answers.Count == 0) return RequirementAccount.Empty;

        var read = pipeline.TryGet<List<string>>(ContextKeys.MasterReadPaths, out var paths)
            ? paths : null;
        var selections = Enum.GetValues<VerificationStation>()
            .ToDictionary(station => station, station => lens.For(pipeline, station));
        var catalogue = selections[VerificationStation.Admission];
        return new RequirementAccount(catalogue.CatalogueVersion, catalogue.Attribution,
        [
            .. Groups(pipeline, answers).Select((group, index) => Settle(
                group, index < RequirementAnswerLog.MaxEntryGroups, answers, selections, read))
        ]);
    }

    /// <summary>The groups the scan stated, in the order it stated them — the entry map
    /// first, since a group nobody mapped is a group nobody located.</summary>
    private static IReadOnlyList<string> Groups(
        PipelineContext pipeline, IReadOnlyList<RequirementAnswer> answers)
    {
        var mapped = pipeline.TryGet<RequestStationMap>(ContextKeys.RequestStationMap, out var map)
            && map is not null ? map.Groups.Select(g => g.Group) : [];
        return [.. mapped.Concat(answers.Select(a => a.Group))
            .Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    private static EntryGroupRequirements Settle(
        string group, bool attempted, IReadOnlyList<RequirementAnswer> answers,
        IReadOnlyDictionary<VerificationStation, VerificationSelection> selections,
        IReadOnlyCollection<string>? read)
    {
        if (!attempted) return new EntryGroupRequirements(group, Attempted: false, []);
        var mine = answers
            .Where(a => string.Equals(a.Group, group, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return new EntryGroupRequirements(group, Attempted: true,
            [.. ReadRows(selections, mine, read), .. WriteRows(selections, mine, read)]);
    }

    /// <summary>Every entry the lens selects for every station: the rows a group owes.</summary>
    private static IEnumerable<RequirementRow> ReadRows(
        IReadOnlyDictionary<VerificationStation, VerificationSelection> selections,
        IReadOnlyList<RequirementAnswer> mine, IReadOnlyCollection<string>? read) =>
        Enum.GetValues<VerificationStation>().SelectMany(station =>
            selections[station].Requirements.Select(entry => Row(station, entry, Read, mine, read)));

    /// <summary>The state-changing operations the group enumerated — rows of its own, never
    /// folded into the read verdict for the same entry.</summary>
    private static IEnumerable<RequirementRow> WriteRows(
        IReadOnlyDictionary<VerificationStation, VerificationSelection> selections,
        IReadOnlyList<RequirementAnswer> mine, IReadOnlyCollection<string>? read) =>
        mine.Where(answer => answer.Operation == Write)
            .Select(answer => (answer.Station, Entry: Entry(selections, answer)))
            .Where(found => found.Entry is not null)
            .Select(found => Row(found.Station, found.Entry!, Write, mine, read));

    private static RequirementRow Row(
        VerificationStation station, VerificationRequirement entry, RequirementOperation operation,
        IReadOnlyList<RequirementAnswer> mine, IReadOnlyCollection<string>? read)
    {
        var answer = mine.LastOrDefault(a => a.Station == station && a.Operation == operation
            && string.Equals(a.RequirementId, entry.Id, StringComparison.OrdinalIgnoreCase));
        if (answer is null)
            return new RequirementRow(station, operation, entry.Id, entry.Level, entry.Text,
                RequirementDisposition.Unanswered, RequirementScope.Member, string.Empty,
                "no answer was stated for this entry");
        var (disposition, citation, note) = RequirementCitation.Settle(answer, read);
        return new RequirementRow(station, operation, entry.Id, entry.Level, entry.Text,
            disposition, answer.Scope, citation, note);
    }

    private static VerificationRequirement? Entry(
        IReadOnlyDictionary<VerificationStation, VerificationSelection> selections,
        RequirementAnswer answer) =>
        selections[answer.Station].Requirements.FirstOrDefault(
            r => string.Equals(r.Id, answer.RequirementId, StringComparison.OrdinalIgnoreCase));
}
