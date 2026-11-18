using AgentSmith.Contracts.Models;

namespace AgentSmith.Server.Services.Admin.Config;

/// <summary>2026-11-17-f3a8c: applies approved configuration changes as a config run on the installation.</summary>
public interface IConfigApplyRun
{
    Task<ConfigApplyReport> RunAsync(ConfigChangeOutcome changes, string person);
}
