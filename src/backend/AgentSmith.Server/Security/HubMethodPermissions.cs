namespace AgentSmith.Server.Security;

/// <summary>
/// p0503c: the permission each hub method needs, as a TABLE keyed by method name rather
/// than an attribute on the method. SignalR's dispatcher collects a hub method's
/// <c>AuthorizeAttribute</c>s at discovery and evaluates them itself through
/// <c>IAuthorizationService</c> on every invocation — outside the middleware pipeline —
/// so an attribute would refuse or throw the moment it landed on a server that registers
/// no authorization services. A table declares the same thing and enforces nothing; the
/// filter of *the hub refuses what the caller may not do* is what reads it.
/// <para>
/// Nothing here decides which RUN a caller may watch. A permission on
/// <c>SubscribeRun</c> says the caller may watch runs, not which one — that is a scope,
/// and a different phase.
/// </para>
/// </summary>
internal static class HubMethodPermissions
{
    private static readonly Dictionary<string, RequiresPermission> ByMethod =
        new(StringComparer.Ordinal)
        {
            ["SubscribeOverview"] = new(Permissions.RunsRead),

            // The system feed is the installation talking about itself — tracker polls,
            // connection state — not a run, so it takes the diagnostics read rather than
            // the run read. Both sit in the reader bundle, so no shipped role changes.
            ["SubscribeSystem"] = new(Permissions.DiagnosticsRead),

            ["SubscribeRun"] = new(Permissions.RunsRead),

            // The pair mutates a process-global refcount other connections share.
            ["ExpandSandbox"] = new(Permissions.RunsWatch),
            ["CollapseSandbox"] = new(Permissions.RunsWatch),

            ["GetTrail"] = new(Permissions.RunsRead),
            ["GetTrailPage"] = new(Permissions.RunsRead),
            ["GetResultMarkdown"] = new(Permissions.RunsRead),
            ["GetPlanMarkdown"] = new(Permissions.RunsRead),
            ["GetSpecMarkdown"] = new(Permissions.RunsRead),
            ["GetAnalyzeMarkdown"] = new(Permissions.RunsRead),
        };

    internal static IReadOnlyCollection<string> MethodNames => ByMethod.Keys;

    /// <summary>
    /// What the named method needs, or null when the table does not name it. A caller
    /// enforcing this refuses the null case: the enumeration test makes a gap a build
    /// failure, so a gap at runtime means a method nobody has classified.
    /// </summary>
    internal static RequiresPermission? For(string methodName) =>
        ByMethod.TryGetValue(methodName, out var required) ? required : null;
}
