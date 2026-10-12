using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// 2026-10-12-6bfab: the process-wide sandbox block as it is stored NOW, read on each call.
/// Throws when it cannot be read — it never answers a failure with an empty block, because an
/// empty block is the WIDE image boundary and a caller has to be able to tell the two apart.
/// </summary>
public interface ISandboxSettingsReader
{
    /// <summary>The current sandbox block; throws when it cannot be read.</summary>
    SandboxGlobalConfig Current();
}
