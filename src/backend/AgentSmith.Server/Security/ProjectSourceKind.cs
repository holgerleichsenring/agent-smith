namespace AgentSmith.Server.Security;

/// <summary>2026-10-15-4a4db: where a route's project comes from — see <see cref="ProjectFrom"/>.</summary>
internal enum ProjectSourceKind
{
    Route,
    Run,
    Dialog,
    Session,
    Image,
    Body,
    AcrossProjects,
}
