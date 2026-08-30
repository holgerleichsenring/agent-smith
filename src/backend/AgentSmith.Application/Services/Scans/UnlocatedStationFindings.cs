using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Scans;

/// <summary>
/// 2026-08-30-18e3: an unlocated station is the finding, not the silence.
/// <para>
/// The live miss this phase exists for was invisible precisely because nothing was said:
/// the scan read the middleware next door, grepped for the configured key, never opened the
/// class where the caller's identity is derived, and delivered a report that read as
/// complete. A named gap is the report that flaw would have shown up in.
/// </para>
/// <para>
/// The rows ship at INFO and never block. Three identical runs of the same repository
/// delivered 25, 26 and 37 findings and the untriaged one looked like the best result, so a
/// coverage statement about the SCAN does not go into the tally the reader reads as
/// vulnerabilities, and never into the ledger the delivery gate reads.
/// </para>
/// </summary>
public static class UnlocatedStationFindings
{
    public const string Role = "entry-station-map";
    public const string Category = "coverage";

    public static IReadOnlyList<SkillObservation> For(RequestStationMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        return [.. map.Unlocated.Select(u => Finding(u.Group, u.Station))];
    }

    private static SkillObservation Finding(string group, StationLocation station) =>
        new(Id: 0, Role: Role,
            Concern: ObservationConcern.Security,
            Description: $"Entry map — '{group}': the {Name(station.Station)} station "
                + $"({Asks(station.Station)}) is not located: {station.Note}",
            Suggestion: $"Read the code that serves '{group}' and state the file and line "
                + $"where {Asks(station.Station)}, or state the input needed to find it.",
            Blocking: false,
            Severity: ObservationSeverity.Info,
            Confidence: 100,
            Rationale: "The scan states its own entry map; this row is a station the map "
                + "left without a location that resolves against the files the scan read.",
            EvidenceMode: EvidenceMode.Potential,
            Category: Category);

    private static string Name(VerificationStation station) =>
        station.ToString().ToLowerInvariant();

    /// <summary>What the station asks, so the row names a question and not a label.</summary>
    private static string Asks(VerificationStation station) => station switch
    {
        VerificationStation.Admission => "a request is admitted — routing, transport, "
            + "protocol, the shape and content accepted",
        VerificationStation.Evidence => "the request's proof of who is making it is taken "
            + "from it — credentials, tokens, cookies, session identifiers",
        VerificationStation.Resolution => "an identity is derived from that evidence and validated",
        VerificationStation.Authority => "what the resolved identity may do is decided",
        VerificationStation.Scope => "which objects the resolved identity may reach is decided",
        _ => "the operation's effect is produced — state changes, output, logs, errors",
    };
}
