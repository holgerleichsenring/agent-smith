namespace AgentSmith.Sandbox.Wire;

/// <summary>
/// Per-pipeline stream-bounding limits shared between the Agent (XADD writer)
/// and the Server (DEL caller) so a single source-edit + Wire version bump
/// changes the bound everywhere.
/// </summary>
public static class StreamLimits
{
    public const int EventStreamMaxLength = 10_000;
}
