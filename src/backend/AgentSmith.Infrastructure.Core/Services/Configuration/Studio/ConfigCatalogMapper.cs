using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Infrastructure.Core.Services.Configuration.Studio;

/// <summary>
/// Projects a full-fidelity <see cref="RawAgentSmithConfig"/> (the YAML-bound
/// document FileConfigStore keeps as the source of truth) onto the thin,
/// editable <see cref="ConfigCatalog"/> the studio and API operate on. The
/// reverse direction is deliberately a PATCH on the raw document (see
/// FileConfigStore) so global blocks, triggers and per-role model routing the
/// studio does not surface survive an export round-trip untouched.
/// </summary>
internal static class ConfigCatalogMapper
{
    public static ConfigCatalog ToCatalog(RawAgentSmithConfig raw) =>
        new(
            Agents: raw.Agents.Select(kv => ToAgent(kv.Key, kv.Value)).ToList(),
            Trackers: raw.Trackers.Select(kv => ToTracker(kv.Key, kv.Value)).ToList(),
            Repos: raw.Repos.Select(kv => ToRepo(kv.Key, kv.Value)).ToList(),
            Projects: raw.Projects.Select(kv => ToProject(kv.Key, kv.Value)).ToList(),
            McpServers: raw.McpServers.Select(kv => ToMcpServer(kv.Key, kv.Value)).ToList(),
            Secrets: raw.Secrets.Keys.Select(k => new SecretEntity(k)).ToList());

    private static AgentEntity ToAgent(string id, AgentConfig agent)
    {
        var models = new Dictionary<string, string> { ["coding"] = agent.Model };
        if (agent.Models is { } registry)
        {
            models["primary"] = registry.Primary.Model;
            models["scout"] = registry.Scout.Model;
            models["planning"] = registry.Planning.Model;
            models["summarization"] = registry.Summarization.Model;
        }
        return new AgentEntity(id, agent.Type, models, agent.ApiKeySecret);
    }

    private static TrackerEntity ToTracker(string id, RawTrackerEntry tracker) =>
        new(id, EnumMemberName(tracker.Type), tracker.Organization, tracker.Project, tracker.Auth);

    private static RepoEntity ToRepo(string id, RawRepoEntry repo) =>
        new(id, repo.Url ?? repo.Path ?? string.Empty, repo.DefaultBranch);

    private static ProjectEntity ToProject(string id, RawProjectEntry project)
    {
        var pipelines = project.Pipelines.Count > 0
            ? project.Pipelines.Select(p => p.Name).ToList()
            : !string.IsNullOrWhiteSpace(project.Pipeline) ? [project.Pipeline] : new List<string>();
        return new ProjectEntity(
            id,
            project.Agent,
            project.Tracker,
            project.Repos.Select(r => r.Ref).ToList(),
            string.IsNullOrWhiteSpace(project.Pipeline) ? null : project.Pipeline,
            pipelines);
    }

    private static McpServerEntity ToMcpServer(string id, RawMcpServerEntry mcp) =>
        new(id, mcp.Transport, mcp.Url, mcp.Auth);

    private static string EnumMemberName(TrackerType type) => type switch
    {
        TrackerType.GitHub => "github",
        TrackerType.GitLab => "gitlab",
        TrackerType.AzureDevOps => "azure_devops",
        TrackerType.Jira => "jira",
        _ => type.ToString().ToLowerInvariant()
    };
}
