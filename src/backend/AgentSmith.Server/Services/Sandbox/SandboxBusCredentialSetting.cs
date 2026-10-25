namespace AgentSmith.Server.Services.Sandbox;

/// <summary>2026-10-25-c5a7e: the SANDBOX_BUS_CREDENTIAL setting, as the DI graph carries it.</summary>
public sealed record SandboxBusCredentialSetting(SandboxBusCredentialMode Mode)
{
    public bool IsShared => Mode == SandboxBusCredentialMode.Shared;
}
