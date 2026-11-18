using System.ComponentModel;
using AgentSmith.Application.Services.Configuration;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Services.Config;
using Microsoft.Extensions.Options;

namespace AgentSmith.Server.Services.Admin.Read;

/// <summary>
/// 2026-11-17-f3a8b: the configuration as runs resolve it — the Config studio's own snapshot, which carries no secret
/// value — and the secrets by name with whether a value is present.
/// </summary>
public sealed class AdminConfigReadTools(
    IConfigurationLoader loader, ServerContext serverContext, IOptions<SandboxGlobalConfig> sandboxGlobal,
    ISandboxResourceResolver resources, IAgentImageResolver agentImages, IConfigStore store, ISecretValues secrets)
{
    [Description("Reads the effective configuration: agents, repositories, trackers, and each project with every "
        + "resolved setting and where it came from (global default, override, run-resolved). Name a project to read only it.")]
    public string ReadConfig(string? project = null)
    {
        var config = loader.LoadConfig(serverContext.ConfigPath);
        var snapshot = ConfigSnapshotMapper.ToSnapshot(config, new ConfigResolutionPass(sandboxGlobal, resources, agentImages, config));
        return string.IsNullOrWhiteSpace(project)
            ? AdminToolJson.Render(snapshot)
            : AdminToolJson.Render(snapshot.Projects.Where(p => ConfigNames.AreSame(p.Name, project)));
    }

    [Description("Lists the configured secrets by name with whether a value is present. A value is never shown.")]
    public string ReadSecrets() =>
        AdminToolJson.Render(store.GetSecrets().Select(s => new { Name = s.Id, Present = secrets.Resolve(s.Id) is not null }));
}
