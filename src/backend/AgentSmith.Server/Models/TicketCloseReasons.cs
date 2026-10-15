namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-10-15-114fa: why a close did not close a pull request or a tracker ticket, beyond the read classes of
/// <see cref="TicketTextReasons"/> — a class, never the host's text.
/// </summary>
public static class TicketCloseReasons
{
    /// <summary>The repository is not configured, or its provider cannot close a pull request.</summary>
    public const string Unsupported = "unsupported";

    /// <summary>The host answered and declined: a status other than 404, 401 or 403, or a tracker that did not close.</summary>
    public const string Refused = "refused";

    /// <summary>The pull request's record already reads closed; it was not tried.</summary>
    public const string AlreadyClosed = "already-closed";
}
