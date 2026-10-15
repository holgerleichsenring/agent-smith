namespace AgentSmith.Server.Security;

/// <summary>
/// 2026-10-15-4a4dc: where a hub invocation's project comes from. A hub method's project rides in an
/// ARGUMENT, a route's in a route value, so the hub keeps its own small source beside its table.
/// </summary>
internal enum HubProjectSource
{
    /// <summary>Installation-wide: only a global hold admits it.</summary>
    Global,

    /// <summary>No single project: a hold in any project admits it, and the method scopes what it hands out.</summary>
    AnyProject,

    /// <summary>The run named by the invocation's FIRST argument: its stored project.</summary>
    RunArgument,

    /// <summary>The dashboard dialog named by the invocation's FIRST argument: its open conversation's project.</summary>
    DialogArgument,
}
