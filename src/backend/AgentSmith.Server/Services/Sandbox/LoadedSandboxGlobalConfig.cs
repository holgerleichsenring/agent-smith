using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Options;

namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// 2026-10-01-283df: the process-wide sandbox block, read from the configuration the first time a
/// consumer asks for its value rather than when the consumer is constructed. Run admission's
/// footprint now reads the browser profile, and admission is composed into graphs that are built
/// long before — or without ever — reading a sandbox setting.
/// </summary>
internal sealed class LoadedSandboxGlobalConfig(IConfigurationLoader loader, ServerContext context)
    : IOptions<SandboxGlobalConfig>
{
    private readonly Lazy<SandboxGlobalConfig> _value = new(() => loader.LoadConfig(context.ConfigPath).Sandbox);

    public SandboxGlobalConfig Value => _value.Value;
}
