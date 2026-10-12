namespace AgentSmith.Contracts.Tickets;

/// <summary>
/// 2026-09-25-3c7ac: the framework's historical label words, in Contracts so a tracker's
/// vocabulary can default to them and recognise them for ever.
/// <para>
/// The stamp's spelling lived in the Application layer, which the four providers do not reference.
/// Moving the LITERAL here changes nothing about who decides with it — that stays where it was.
/// </para>
/// </summary>
public static class TicketLabels
{
    /// <summary>The stamp a filing writes: an approved specification exists for this ticket.</summary>
    public const string ApprovedSetStamp = "phase-spec:approved";

    /// <summary>2026-10-12-967aa: the stamp's spec-named spelling, recognised beside the historical
    /// one so a filing by a later release routes on every replica.</summary>
    public const string AcceptedApprovedSetStamp = "spec:approved";

    /// <summary>2026-10-12-967aa: the spec-named word a person types to bind a ticket to spec
    /// execution, read beside "phase" for good — both sit on boards this framework does not own.</summary>
    public const string AcceptedSpecLabel = "spec";

    /// <summary>2026-10-12-967aa: the spec-named record label, refused beside "phase-epic".</summary>
    public const string AcceptedSpecEpicLabel = "spec-epic";

    /// <summary>2026-10-12-967aa: the spec-named parent stamp, read beside "phase-parent:".</summary>
    public const string AcceptedParentPrefix = "spec-parent:";
}
