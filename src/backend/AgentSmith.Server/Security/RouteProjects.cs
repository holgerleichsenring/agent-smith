namespace AgentSmith.Server.Security;

/// <summary>
/// 2026-10-15-4a4db: what a route's <see cref="ProjectFrom"/> resolved to for one request — the resource
/// is missing, it belongs to no project yet, or the projects the caller must hold the route's permissions in.
/// </summary>
internal sealed record RouteProjects(bool Exists, IReadOnlyList<string> Names)
{
    internal static RouteProjects Missing { get; } = new(false, []);

    internal static RouteProjects Unscoped { get; } = new(true, []);

    internal static RouteProjects Of(IEnumerable<string?> names) =>
        new(true, [.. names.Where(name => !string.IsNullOrEmpty(name)).Select(name => name!)]);

    /// <summary>A stored project column: no row is missing, an empty one is not yet grounded.</summary>
    internal static RouteProjects Stored(string? project) => project is null ? Missing : Of([project]);
}
