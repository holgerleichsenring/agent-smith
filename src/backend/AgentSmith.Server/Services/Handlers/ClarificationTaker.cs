using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.Handlers;

/// <summary>
/// 2026-10-02-5ab2d: one click on a clarification's buttons, on any platform. It takes the
/// channel's pending command; Confirm with nothing pending — expired, gone, or already taken by
/// the other click — tells the person to send it again instead of answering nothing; Help shows
/// help whether or not a command was pending, because help needs none.
/// </summary>
public sealed class ClarificationTaker(ClarificationStateManager state, HelpHandler help)
{
    public const string Confirm = "confirm";

    /// <summary>The command to dispatch, or null when the click is fully answered here.</summary>
    public async Task<PendingClarification?> TakeAsync(
        string platform, string channelId, string answer, CancellationToken ct)
    {
        var pending = await state.TakeAsync(platform, channelId, ct);
        if (answer != Confirm)
        {
            await help.SendHelpAsync(platform, channelId, ct);
            return null;
        }
        if (pending is null) await help.SendClarificationExpiredAsync(platform, channelId, ct);
        return pending;
    }
}
