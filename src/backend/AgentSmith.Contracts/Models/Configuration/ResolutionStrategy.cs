using System.Runtime.Serialization;

namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// How a webhook handler decides which agent-smith project owns an incoming ticket.
/// One incoming ticket may match more than one project (e.g. two projects tagged with
/// the same label) — in that case every match spawns its own pipeline run.
/// </summary>
public enum ResolutionStrategy
{
    /// <summary>Ticket label/tag equals the configured value (case-insensitive).</summary>
    Tag,

    /// <summary>ADO area path: hierarchical prefix match. Supports both \ and / separators.</summary>
    [EnumMember(Value = "area_path")] AreaPath,

    /// <summary>Source-repo URL match. Requires project.Repos.Count == 1.</summary>
    Repo,

    /// <summary>Email tracker: the to-address on the incoming mail equals the configured value. Reserved for p0141.</summary>
    [EnumMember(Value = "to_address")] ToAddress
}
