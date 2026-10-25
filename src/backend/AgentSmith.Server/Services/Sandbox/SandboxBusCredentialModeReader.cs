namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// 2026-10-25-c5a7e: reads SANDBOX_BUS_CREDENTIAL. Only the literal "shared" selects the
/// shared credential; unset or anything else is per-job, so a typo never widens access.
/// </summary>
public static class SandboxBusCredentialModeReader
{
    public const string Variable = "SANDBOX_BUS_CREDENTIAL";
    public const string SharedValue = "shared";
    public const string PerJobValue = "per-job";

    public static SandboxBusCredentialSetting Read(string? value) => new(
        string.Equals(value?.Trim(), SharedValue, StringComparison.OrdinalIgnoreCase)
            ? SandboxBusCredentialMode.Shared
            : SandboxBusCredentialMode.PerJob);
}
