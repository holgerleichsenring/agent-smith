namespace AgentSmith.Contracts.Tickets;

/// <summary>
/// 2026-09-18-b4f0: WHAT a ticket agent-smith files IS, as the call site filing it knows
/// statically. It is a property of the call site and never of the content: the work ticket
/// an epic files and the lone phase ticket carry IDENTICAL labels, so nothing downstream
/// can tell them apart after the fact.
/// <para>
/// A role is turned into the tracker's own work-item kind before the create — the role
/// itself never crosses <c>ITicketProvider</c>, which speaks tracker vocabulary only. The
/// member names ARE the wire keys of the tracker's <c>work_item_kinds</c> map, parsed
/// case-insensitively: work / bug / phase / chat.
/// </para>
/// <para>
/// 2026-09-22-b3d7: the <c>record</c> role went with the slice records it existed for. A live
/// configuration that still maps it keeps validating and is warned about as an unknown role,
/// which is what it now is: a key that files nothing.
/// </para>
/// </summary>
public enum TicketFilingRole
{
    /// <summary>An approved cut's one work ticket — the run, the branch, the pull request.</summary>
    Work,

    /// <summary>A confirmed bug. The one filing both trackers ship a dedicated native type for.</summary>
    Bug,

    /// <summary>A lone approved phase. Its own role because an installation may map a single
    /// phase and a cut's work ticket to different native types.</summary>
    Phase,

    /// <summary>A ticket a chat request asked for.</summary>
    Chat,
}
