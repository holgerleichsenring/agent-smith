using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-08-30-3c12: the rule behind the answer tool — what an answer must carry before the
/// run will hold it at all, and the payload bound it is held to.
/// <para>
/// The entry is looked up in the lens selection for the station rather than taken on the
/// model's word: the model answers the entries it is HANDED and does not choose them, so an
/// id nobody selected is refused where the model can still fix it. The same goes for a
/// cannot-answer with no missing input — silence dressed as a verdict.
/// </para>
/// </summary>
public sealed class RequirementAnswerRecorder(IVerificationLens lens)
{
    /// <summary>How many members one group-wide claim may generalise over.</summary>
    public const int MaxCitedMembers = 12;

    private const int MaxFreeTextLength = 300;

    private static readonly Dictionary<string, RequirementDisposition> Verdicts =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["met"] = RequirementDisposition.Met,
            ["unmet"] = RequirementDisposition.Unmet,
            ["cannot_answer"] = RequirementDisposition.CannotAnswer,
        };

    public string Record(PipelineContext run, RequirementAnswerLog log, RequirementAnswerRequest request)
    {
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Group))
            return "Error: group is required — name the family of entry points this answer is about.";
        if (!Enum.TryParse<VerificationStation>(request.Station, ignoreCase: true, out var station))
            return Unknown("station", request.Station, Enum.GetNames<VerificationStation>());
        if (!Enum.TryParse<RequirementOperation>(request.Operation, ignoreCase: true, out var operation))
            return Unknown("operation", request.Operation, Enum.GetNames<RequirementOperation>());
        if (!Verdicts.TryGetValue(request.Verdict?.Trim() ?? string.Empty, out var verdict))
            return Unknown("verdict", request.Verdict, [.. Verdicts.Keys]);
        var entry = Entry(run, station, request.RequirementId);
        return entry is null
            ? NotSelected(request.RequirementId, station)
            : Hold(log, request, entry, station, operation, verdict);
    }

    private string Hold(
        RequirementAnswerLog log, RequirementAnswerRequest request, VerificationRequirement entry,
        VerificationStation station, RequirementOperation operation, RequirementDisposition verdict)
    {
        if (verdict == RequirementDisposition.CannotAnswer && string.IsNullOrWhiteSpace(request.MissingInput))
            return "Error: a cannot_answer verdict must name the input it would have needed "
                + "(missing_input) — an entry nobody could decide is not an entry nobody stated.";
        var groupWide = string.Equals(request.Scope, "group", StringComparison.OrdinalIgnoreCase);
        var answer = new RequirementAnswer(
            request.Group.Trim(), station, entry.Id, operation, verdict,
            groupWide ? RequirementScope.GroupWide : RequirementScope.Member,
            Clip(request.File), request.StartLine, Members(request.CoversMembers),
            Clip(request.MissingInput), string.Empty);
        return log.Record(answer)
            ? $"Recorded: {answer.Group} / {station} / {entry.Id} on {operation} — {verdict}."
            : $"Not recorded: this run answers at most {RequirementAnswerLog.MaxEntryGroups} entry "
                + $"group(s) and '{answer.Group}' is beyond that. It is reported NOT ATTEMPTED.";
    }

    private VerificationRequirement? Entry(PipelineContext run, VerificationStation station, string? id) =>
        lens.For(run, station).Requirements.FirstOrDefault(
            r => string.Equals(r.Id, id?.Trim(), StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyList<string> Members(string? cited) =>
        string.IsNullOrWhiteSpace(cited)
            ? []
            : [.. cited.Split([',', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Take(MaxCitedMembers)];

    private static string? Clip(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null
            : text.Trim().Length <= MaxFreeTextLength ? text.Trim()
            : text.Trim()[..MaxFreeTextLength];

    private static string NotSelected(string? id, VerificationStation station) =>
        $"Error: '{id}' is not one of the entries selected for the {station} station. Call "
        + $"{RequirementCatalogueToolHost.ToolName} for that station and answer what it lists.";

    private static string Unknown(string field, string? given, IReadOnlyList<string> allowed) =>
        $"Error: unknown {field} '{given}'. Use one of: "
        + string.Join(", ", allowed).ToLowerInvariant() + ".";
}
