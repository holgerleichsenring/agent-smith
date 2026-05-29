namespace AgentSmith.Application.Services.Loop;

/// <summary>
/// p0177: tool profile the master selects when spawning a sub-agent.
/// Determines which read/write tools the child sees through ToolKit —
/// the policy layer enforces the actual subset; this enum is the
/// operator-visible label flowing through SubAgentSpec.
/// </summary>
public enum ToolProfile
{
    /// <summary>Read-only investigator — read_file + list_files. No write, no shell.</summary>
    Investigator = 0,
    /// <summary>Sandbox-capable verifier — read + sandboxed run_command. No write_file.</summary>
    Verifier = 1,
    /// <summary>Full read/write/run set, scoped to the run sandbox. The master's own profile.</summary>
    FullStack = 2,
}
