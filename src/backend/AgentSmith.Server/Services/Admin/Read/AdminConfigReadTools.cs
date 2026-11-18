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
    ISandboxResourceResolver resources, IAgentImageResolver agentImages, ISecretValues secrets,
    Secrets.SecretValuesReader values)
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

    [Description("Lists the configured secrets by name: whether a value is present and where it comes from — the "
        + "environment (which wins), a value stored in the admin area (who set it, when, and whether the running master "
        + "key opens it), or missing. A value is never shown.")]
    public async Task<string> ReadSecrets(CancellationToken cancellationToken = default)
    {
        var view = await values.ReadAsync(cancellationToken);
        return AdminToolJson.Render(new
        {
            view.MasterKeyPresent, view.MasterKeyProblem, view.MasterKeySource,
            Secrets = view.Secrets.Select(s => new
            {
                s.Name, Present = secrets.Resolve(s.Name) is not null,
                Source = s.FromEnvironment ? "environment" : s.Stored is { Readable: true } ? "stored" : "missing",
                StoredBy = s.Stored?.SetBy, StoredAt = s.Stored?.SetAt, StoredKeyCurrent = s.Stored?.KeyCurrent,
            }),
            OrphanedStoredValues = view.Orphans.Select(o => o.Name),
        });
    }
}
