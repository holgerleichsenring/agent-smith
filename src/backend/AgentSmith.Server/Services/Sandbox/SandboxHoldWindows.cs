namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// 2026-09-22-2d11a: how long a conversation's sandboxes are held, as ONE scan
/// resolved it — the process-wide value plus the projects that set their own. A
/// scan resolves this once and then answers per sandbox from it, because the
/// configuration loader re-assembles the whole catalog from the document store on
/// every call and a per-container read would be a self-inflicted load.
/// </summary>
/// <param name="ProcessWide">The value every project without an override gets.</param>
/// <param name="ByProject">The projects that named their own window, by project name.</param>
public sealed record SandboxHoldWindows(
    TimeSpan ProcessWide,
    IReadOnlyDictionary<string, TimeSpan> ByProject)
{
    /// <summary>The window for the project a held conversation belongs to.</summary>
    public TimeSpan For(string project) =>
        ByProject.TryGetValue(project, out var window) ? window : ProcessWide;
}
