using AgentSmith.Server.Contracts;
using AgentSmith.Server.Services.Sandbox;
using AgentSmith.Server.Services.Startup;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// 2026-10-25-c5a7e: the per-job sandbox Redis credential, for the two backends whose
/// sandboxes reach Redis themselves. SANDBOX_BUS_CREDENTIAL is read once, here, for both.
/// </summary>
internal static class SandboxBusCredentialRegistrations
{
    internal static void Register(IServiceCollection services)
    {
        services.AddSingleton(SandboxBusCredentialModeReader.Read(
            Environment.GetEnvironmentVariable(SandboxBusCredentialModeReader.Variable)));
        services.AddSingleton<SandboxBusCredentials>();
        services.AddSingleton<IStartupProbe, SandboxBusCredentialProbe>();
    }
}
