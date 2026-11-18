using AgentSmith.Contracts.Models;

namespace AgentSmith.Server.Services.Admin.Secrets;

/// <summary>2026-11-17-f3a8d: a secret value set, deleted or re-wrapped as a secret run on the installation.</summary>
public interface ISecretWriteRun
{
    Task<SecretWriteReport> RunAsync(SecretWriteRequest request);
}
