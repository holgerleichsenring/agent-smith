using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-08-30-03e1: the rule behind the citation tool — what a finding must carry before
/// this path will hold it, and the payload bound it is held to.
/// <para>
/// EVERY REFUSAL HERE POINTS SOMEWHERE. A finding that names no entry, an id the standard
/// does not carry at this station, a group beyond the cap: each is answered with the
/// ordinary observation path, which delivers the finding unchanged. Refusing a finding and
/// leaving it nowhere to go would suppress exactly the class this shape exists to recover.
/// </para>
/// <para>
/// The id is looked up against the FULL floor set the lens classifies for the station, not
/// a bounded slice of it — a bound here would refuse a real finding for the crime of
/// citing the thirteenth-ranked entry.
/// </para>
/// </summary>
public sealed class CitedFindingRecorder(IVerificationLens lens)
{
    /// <summary>How many members one group-wide finding may generalise over.</summary>
    public const int MaxCitedMembers = 12;

    private const int MaxFreeTextLength = 300;

    public string Record(PipelineContext run, CitedFindingLog log, CitedFindingRequest request)
    {
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Group))
            return "Error: group is required — name the family of entry points this finding is about.";
        if (!Enum.TryParse<VerificationStation>(request.Station, ignoreCase: true, out var station))
            return Unknown("station", request.Station, Enum.GetNames<VerificationStation>());
        if (string.IsNullOrWhiteSpace(request.RequirementId)) return NoRequirement;
        var entry = Entry(run, station, request.RequirementId);
        return entry is null ? NotCarried(request.RequirementId, station)
            : Hold(log, request, entry, station);
    }

    private static string Hold(
        CitedFindingLog log, CitedFindingRequest request, VerificationRequirement entry,
        VerificationStation station)
    {
        var groupWide = string.Equals(request.Scope, "group", StringComparison.OrdinalIgnoreCase);
        var finding = new CitedFinding(
            request.Group.Trim(), station, entry.Id, entry.Level, entry.Text,
            groupWide ? RequirementScope.GroupWide : RequirementScope.Member,
            Clip(request.File), request.StartLine, Members(request.CoversMembers),
            Clip(request.Detail) ?? string.Empty);
        return log.Record(finding)
            ? $"Recorded: {finding.Group} / {station} / {entry.Id} — {finding.Detail}"
            : $"Not recorded: this run accounts for at most {CitedFindingLog.MaxEntryGroups} entry "
                + $"group(s) and '{finding.Group}' is beyond that, so it is reported NOT ATTEMPTED. "
                + "Put this finding in your observation array — it is delivered unchanged there.";
    }

    private VerificationRequirement? Entry(
        PipelineContext run, VerificationStation station, string id) =>
        lens.For(run, station).Requirements.FirstOrDefault(
            requirement => string.Equals(requirement.Id, id.Trim(), StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyList<string> Members(string? cited) =>
        string.IsNullOrWhiteSpace(cited)
            ? []
            : [.. cited.Split([',', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Take(MaxCitedMembers)];

    private static string? Clip(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null
            : text.Trim().Length <= MaxFreeTextLength ? text.Trim()
            : text.Trim()[..MaxFreeTextLength];

    private const string NoRequirement =
        "Error: this path is only for a finding that names the entry of the standard it "
        + "breaks. Look one up with " + RequirementLookupToolHost.ToolName + ", or — if no "
        + "entry covers what you found — report it in your observation array, where it is "
        + "delivered exactly as any other finding is.";

    private static string NotCarried(string id, VerificationStation station) =>
        $"Error: '{id}' is not an entry the standard carries at the {station} station in this "
        + $"release. Call {RequirementLookupToolHost.ToolName} for that station, or report "
        + "the finding in your observation array.";

    private static string Unknown(string field, string? given, IReadOnlyList<string> allowed) =>
        $"Error: unknown {field} '{given}'. Use one of: "
        + string.Join(", ", allowed).ToLowerInvariant() + ".";
}
