namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-09-20-3af8: the conversation an uploaded image belongs to — or why it belongs to none.
/// The two refusals are kept apart because the page answers them differently: one is somebody
/// else's conversation, the other is a conversation that could not be started because no
/// project was named for it.
/// </summary>
public sealed record SpecDialogUploadTarget(string? SessionId, bool BelongsToAnother)
{
    /// <summary>A session is open on this dialog id and the caller is not its owner.</summary>
    public static readonly SpecDialogUploadTarget Foreign = new(null, true);

    /// <summary>No session was open and none could be opened.</summary>
    public static readonly SpecDialogUploadTarget Unopened = new(null, false);

    public static SpecDialogUploadTarget On(string sessionId) => new(sessionId, false);
}
