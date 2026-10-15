namespace AgentSmith.Server.Security;

/// <summary>
/// 2026-10-15-4a4db: where the project a route acts in comes from, declared beside the route's
/// permission. A route needing a project-scopable permission states one, so a caller holding it only
/// in other projects is answered as if the resource did not exist. <see cref="Body"/> is checked by
/// the handler (the dialog id rides in the body); <see cref="AcrossProjects"/> marks a list or an
/// aggregate, which filters its rows by caller (2026-10-15-4a4dd).
/// </summary>
internal sealed record ProjectFrom(ProjectSourceKind Kind, string Parameter)
{
    /// <summary>A route value naming the project itself — <c>{project}</c> or <c>{name}</c>.</summary>
    internal static ProjectFrom Route(string name) => new(ProjectSourceKind.Route, name);

    /// <summary>The run named by <c>{runId}</c>: its stored project.</summary>
    internal static ProjectFrom Run { get; } = new(ProjectSourceKind.Run, "runId");

    /// <summary>The open dashboard conversation on the dialog id (route value or query), plus a query project.</summary>
    internal static ProjectFrom Dialog { get; } = new(ProjectSourceKind.Dialog, "dialogId");

    /// <summary>The dashboard conversation named by <c>{sessionId}</c>.</summary>
    internal static ProjectFrom Session { get; } = new(ProjectSourceKind.Session, "sessionId");

    /// <summary>The conversation an image named by <c>{imageId}</c> belongs to.</summary>
    internal static ProjectFrom Image { get; } = new(ProjectSourceKind.Image, "imageId");

    /// <summary>The handler reads the project from the body — POST /api/spec-dialog/messages alone.</summary>
    internal static ProjectFrom Body { get; } = new(ProjectSourceKind.Body, string.Empty);

    /// <summary>A list or aggregate across projects, filtered by the caller's projects (2026-10-15-4a4dd).</summary>
    internal static ProjectFrom AcrossProjects { get; } = new(ProjectSourceKind.AcrossProjects, string.Empty);

    /// <summary>2026-10-15-4a4dc: /hub/jobs alone — admitted on any project, its filter scopes each invocation.</summary>
    internal static ProjectFrom Hub { get; } = new(ProjectSourceKind.Hub, string.Empty);

    /// <summary>Whether the route itself is checked before the handler runs.</summary>
    internal bool IsCheckedAtTheRoute => Kind is not (ProjectSourceKind.Body or ProjectSourceKind.AcrossProjects or ProjectSourceKind.Hub);

    /// <summary>The spelling the route golden pins.</summary>
    internal string Wire => Kind switch
    {
        ProjectSourceKind.Route => $"route:{Parameter}",
        ProjectSourceKind.AcrossProjects => "across",
        _ => Kind.ToString().ToLowerInvariant(),
    };
}
