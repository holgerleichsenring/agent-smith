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
}
