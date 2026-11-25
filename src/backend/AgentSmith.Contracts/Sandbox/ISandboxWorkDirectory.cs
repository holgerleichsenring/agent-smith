namespace AgentSmith.Contracts.Sandbox;

/// <summary>
/// 2026-11-25-9992c: where a sandbox's repository root is — the directory a step with no
/// working directory runs in and a relative path resolves under. A backend that holds one
/// tree names the workspace root; a run's repository sandbox names /work/&lt;repo&gt;.
/// </summary>
public interface ISandboxWorkDirectory
{
    string WorkDirectory { get; }
}
