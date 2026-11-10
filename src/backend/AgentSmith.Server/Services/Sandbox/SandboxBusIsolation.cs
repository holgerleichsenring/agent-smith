using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Server.Services.Sandbox;

/// <summary>2026-11-10-4778: the installation's bus mode, as SANDBOX_BUS_CREDENTIAL set it.</summary>
public sealed class SandboxBusIsolation(SandboxBusCredentialSetting setting) : ISandboxBusIsolation
{
    public bool IsShared => setting.IsShared;
}
