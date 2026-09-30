namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// The agent's model routing: the catalog of models it may call and the roles that pick from
/// it. <see cref="ModelRoleChain"/> is the one reader that turns the two into a call.
/// </summary>
public sealed partial class AgentConfig
{
    /// <summary>The models this agent may call, by entry name; a role picks one with <c>use:</c>.</summary>
    public Dictionary<string, CatalogModel> Catalog { get; set; } = new();

    public ModelRegistryConfig? Models { get; set; }
}
