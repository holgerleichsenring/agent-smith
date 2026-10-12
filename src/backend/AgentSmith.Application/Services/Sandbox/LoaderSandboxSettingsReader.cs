using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Options;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// 2026-10-12-6bfab: the CLI's sandbox block — the configuration file, re-read through the
/// loader on each call when a config path was given (the file loader throws on a failure),
/// otherwise the composed options. The server replaces this with a reader over its config
/// store, because the database loader answers a failure with an empty configuration.
/// </summary>
public sealed class LoaderSandboxSettingsReader(
    IConfigurationLoader? loader = null,
    ServerContext? context = null,
    IOptions<SandboxGlobalConfig>? options = null) : ISandboxSettingsReader
{
    public SandboxGlobalConfig Current() =>
        loader is not null && context is not null
            ? loader.LoadConfig(context.ConfigPath).Sandbox
            : options?.Value ?? new SandboxGlobalConfig();
}
