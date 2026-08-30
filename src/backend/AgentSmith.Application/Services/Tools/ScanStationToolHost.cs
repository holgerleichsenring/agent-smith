using System.ComponentModel;
using AgentSmith.Application.Models;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-08-30-18e3: the carrier for the inventory the scan master already performs.
/// <para>
/// The map travels as its OWN tool call, never inside the answer. The master's final answer
/// is a closed contract — a single JSON array, with everything outside it discarded — and
/// wrapping it in an object would put the run back on the branch where the merge reads
/// nothing and ships raw untriaged scanner output. One station per call, so a group is
/// stated a piece at a time and nothing has to be re-emitted to correct one row.
/// </para>
/// </summary>
public sealed class ScanStationToolHost(StationClaimLog log) : IToolHost
{
    public const string ToolName = "record_entry_station";

    public IEnumerable<AIFunction> GetTools(SkillExecutionPhase? phase, string? investigatorMode)
    {
        _ = phase;
        _ = investigatorMode;
        return [AIFunctionFactory.Create(RecordEntryStation, name: ToolName)];
    }

    [Description(
        "Records where ONE station of ONE entry group of this system lives. Call it once per "
        + "station per entry group, six calls per group. Either give the file and line where "
        + "the station is implemented — by whatever construct answers it here: a decorator, a "
        + "guard, a filter, an extractor, an interceptor, an attribute or a hand-written call "
        + "— or give not_located_reason naming the input you would have needed. A station "
        + "with no call at all is reported as a gap.")]
    public string RecordEntryStation(
        [Description("The entry group this station belongs to, in your own words — the name "
            + "you would use for this family of entry points (e.g. 'public REST API', "
            + "'background message consumers').")]
        string group,
        [Description("One of: admission (routing, transport, protocol, the shape accepted), "
            + "evidence (what the request carries as proof of who is making it), resolution "
            + "(how an identity is derived from that evidence and validated), authority (what "
            + "the resolved identity may do), scope (which objects it may reach), effect (what "
            + "the operation does and produces).")]
        string station,
        [Description("Repo-relative path of the file where this station is implemented. It "
            + "must be a file you READ this run — a path you only inferred does not locate "
            + "anything. Empty when the station is not located.")]
        string file,
        [Description("The line in that file where the station is implemented. 0 when the "
            + "station is not located.")]
        int start_line,
        [Description("When the station is not located, the input you would have needed to "
            + "locate it, or the reason this system has no such station. Empty when a file "
            + "and line are given.")]
        string not_located_reason = "")
    {
        if (string.IsNullOrWhiteSpace(group))
            return "Error: group is required — name the family of entry points this station belongs to.";
        if (!Enum.TryParse<VerificationStation>(station, ignoreCase: true, out var parsed))
            return $"Error: unknown station '{station}'. Use one of: "
                + string.Join(", ", Enum.GetNames<VerificationStation>()).ToLowerInvariant() + ".";

        var located = !string.IsNullOrWhiteSpace(file) && start_line > 0;
        log.Record(new StationClaim(
            group.Trim(), parsed, located ? file.Trim() : null, located ? start_line : 0,
            located ? null : Reason(not_located_reason)));
        return located
            ? $"Recorded: {group.Trim()} / {parsed} at {file.Trim()}:{start_line}."
            : $"Recorded: {group.Trim()} / {parsed} NOT located — {Reason(not_located_reason)}";
    }

    private static string Reason(string stated) =>
        string.IsNullOrWhiteSpace(stated)
            ? "no reason stated"
            : stated.Trim();
}
