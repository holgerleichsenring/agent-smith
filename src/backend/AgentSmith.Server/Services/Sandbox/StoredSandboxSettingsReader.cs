using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;

namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// 2026-10-12-6bfab: the server's sandbox block, read from the config store's assembled
/// document — the one a Studio save reloads on this replica and the config-epoch watch
/// reloads on every other. Not through the configuration loader: that one answers a database
/// failure with an empty configuration, which for the image boundary is the wide default. The
/// store keeps its last document when a reload fails, and throws when it never had one.
/// </summary>
public sealed class StoredSandboxSettingsReader(IConfigStore store) : ISandboxSettingsReader
{
    public SandboxGlobalConfig Current() =>
        store.GetSetting(ConfigDocTypes.Sandbox) as SandboxGlobalConfig
            ?? throw new InvalidOperationException("The stored sandbox settings are not a sandbox block.");
}
