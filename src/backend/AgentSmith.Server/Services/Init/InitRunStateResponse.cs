namespace AgentSmith.Server.Services.Init;

/// <summary>
/// 2026-10-02-5f89d: the wire shape of GET /api/projects/{name}/init — the project's live
/// init run and what it is doing: <c>queued</c> (waiting for a slot), <c>running</c>, or
/// <c>cancelling</c> (flagged, its sandboxes not yet ended). A paused run reads running:
/// it is live, and the run page says what it waits for.
/// </summary>
public sealed record InitRunStateResponse(string RunId, string State)
{
    public const string Queued = "queued";
    public const string Running = "running";
    public const string Cancelling = "cancelling";
}
