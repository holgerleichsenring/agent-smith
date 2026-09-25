namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-09-25-8e51d: the specification a person APPROVED for the ticket this conversation belongs
/// to, as the pane draws it.
/// <para>
/// Labelled with the approval it is, deliberately. The branch is the only set a RUN reads, and
/// for a ticket already worked the two can differ — the branch may be several revisions and a
/// positional merge ahead. A reader must never take this for what the run is doing.
/// </para>
/// <para>
/// The phases travel in the shape the PROPOSAL pane already draws, so the conversation shows an
/// approved phase exactly as it shows a proposed one and there is one mirror to keep in step.
/// </para>
/// </summary>
public sealed record ApprovedSetView(
    string Key,
    string Tracker,
    DateTimeOffset? ApprovedAt,
    string? ApprovedBy,
    string? ApprovedInConversation,
    IReadOnlyList<string> Repositories,
    IReadOnlyList<SpecDialogPhaseView> Phases);
