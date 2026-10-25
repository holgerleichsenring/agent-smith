namespace AgentSmith.Server.Services.Sandbox;

/// <summary>2026-10-25-c5a7e: which Redis credential a sandbox is handed.</summary>
public enum SandboxBusCredentialMode
{
    /// <summary>An ACL user per job, limited to that job's keys (the default).</summary>
    PerJob,

    /// <summary>The server's own Redis URL, as before 2026-10-25-c5a7e.</summary>
    Shared,
}
