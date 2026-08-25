namespace AgentSmith.Infrastructure.Core.Services.Configuration.Studio;

/// <summary>p0349: the config doc type names — the taxonomy keys stored in config_entity.type.</summary>
public static class ConfigDocTypes
{
    /// <summary>The fixed id every SINGLETON doc is stored under — a singleton has one row.</summary>
    public const string SingletonId = "default";

    public const string Agent = "agent";
    public const string Tracker = "tracker";
    public const string Connection = "connection";
    public const string Repo = "repo";
    public const string Project = "project";
    public const string McpServer = "mcp_server";
    public const string Secret = "secret";
    public const string PipelineTrigger = "pipeline_trigger";

    public const string Registries = "registries";
    public const string Queue = "queue";
    public const string Skills = "skills";
    public const string PrimaryProvider = "primary_provider";
    public const string Limits = "limits";
    public const string PipelineStorage = "pipeline_storage";
    public const string PipelineDataFlow = "pipeline_data_flow";
    public const string Deployment = "deployment";
    public const string Sandbox = "sandbox";
    public const string Orchestrator = "orchestrator";
    public const string Dialogue = "dialogue";
    public const string Persistence = "persistence";
    public const string PipelineCostCap = "pipeline_cost_cap";

    // 2026-08-25-1806: role names, their permission bundles and the two claims they are
    // read from — application configuration, not bootstrap.
    public const string RoleMapping = "role_mapping";
}
