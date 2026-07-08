namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>
/// A spec-dialog design session (p0315a). Lives in the relational
/// system-of-record — NOT in Redis — because this deployment's Redis is
/// volatile and a design transcript must survive a flush/restart. Keyed by
/// chat thread (Platform + ThreadId) so parallel threads stay isolated;
/// SessionId is the short handle used by "/spec resume &lt;id&gt;".
/// </summary>
public sealed class SpecDialogSession : EntityBase
{
    public long Id { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string ChannelId { get; set; } = string.Empty;
    public string ThreadId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;

    /// <summary>Active scope: the project the session is grounded on.</summary>
    public string Project { get; set; } = string.Empty;

    /// <summary>JSON array of repo names within the active scope.</summary>
    public string ReposJson { get; set; } = "[]";

    /// <summary>JSON array of ordered user/assistant transcript turns.</summary>
    public string TranscriptJson { get; set; } = "[]";

    /// <summary>False once the session is closed or forked away from.</summary>
    public bool IsOpen { get; set; } = true;

    public DateTimeOffset LastActivityAt { get; set; }
}
