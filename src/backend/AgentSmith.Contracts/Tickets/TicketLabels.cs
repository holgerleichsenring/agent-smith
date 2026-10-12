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
    /// <summary>The stamp a filing writes: an approved specification exists for this ticket.
    /// 2026-10-12-967ab: spec-named; <see cref="AcceptedApprovedSetStamp"/> is what filings wrote before.</summary>
    public const string ApprovedSetStamp = "spec:approved";

    /// <summary>2026-10-12-967ab: the stamp's historical spelling, recognised for good — tickets carrying
    /// it sit on boards this framework does not own.</summary>
    public const string AcceptedApprovedSetStamp = "phase-spec:approved";

    /// <summary>2026-10-12-967ab: the historical word a person typed to bind a ticket to spec execution,
    /// read beside "spec" for good — both sit on boards this framework does not own.</summary>
    public const string AcceptedSpecLabel = "phase";

    /// <summary>2026-10-12-967ab: the historical record label, refused beside "spec-epic" for good.</summary>
    public const string AcceptedSpecEpicLabel = "phase-epic";

    /// <summary>2026-10-12-967ab: the historical parent stamp, read beside "spec-parent:" for good.</summary>
    public const string AcceptedParentPrefix = "phase-parent:";
}
