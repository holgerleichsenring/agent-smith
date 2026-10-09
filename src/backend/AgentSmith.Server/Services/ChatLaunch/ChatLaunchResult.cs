namespace AgentSmith.Server.Services.ChatLaunch;

/// <summary>
/// What came of a run a person asked for in chat: started under a run id, waiting in the
/// capacity queue under a run id with the reason it waits, or refused with the reason.
/// </summary>
public sealed record ChatLaunchResult(
    string? RunId, string? WaitReason, string? Refusal, bool TakenByAnother = false)
{
    public static ChatLaunchResult Started(string runId) => new(runId, null, null);

    public static ChatLaunchResult Waiting(string runId, string reason) => new(runId, reason, null);

    public static ChatLaunchResult Refused(string reason) => new(null, null, reason);

    /// <summary>2026-10-08-e8b9b: refused because another claimer holds the ticket — a refusal to the
    /// person asking in chat, but for a rework the act is served by whoever holds it.</summary>
    public static ChatLaunchResult Taken() => new(null, null, "This ticket already has a run in flight.", true);
}
