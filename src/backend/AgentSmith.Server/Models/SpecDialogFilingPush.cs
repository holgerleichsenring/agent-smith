using AgentSmith.Server.Services.SpecDialog;

namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-09-15-6d9c: what the filing attempt ACTUALLY created — the payload of the hub's
/// "SpecDialogFiled" push. <paramref name="Filed"/> carries every ticket that was created
/// even when <paramref name="Error"/> is set, because a partial epic must never silently
/// lose the children it did create. <paramref name="Notes"/> says what went wrong without
/// unfiling anything, such as a child the tracker would not link to its parent.
/// </summary>
public sealed record SpecDialogFilingPush(
    string DialogId, IReadOnlyList<FiledTicket> Filed, string? Error, DateTimeOffset At,
    IReadOnlyList<string> Notes);
