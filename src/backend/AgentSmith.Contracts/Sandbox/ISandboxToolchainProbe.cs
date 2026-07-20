namespace AgentSmith.Contracts.Sandbox;

/// <summary>
/// p0356: probes what each sandbox's toolchain image actually provides (shell,
/// language SDK versions, git) with a single command at master start, and
/// distills the result into a capability section for the master context — the
/// master KNOWS what it can run (scripts, codemods, compilers) instead of
/// guessing from the repo language. Returns null when nothing could be probed
/// (the section is simply absent — never a fabricated inventory).
/// </summary>
public interface ISandboxToolchainProbe
{
    Task<string?> ProbeAsync(
        IReadOnlyDictionary<string, ISandbox> sandboxes,
        IReadOnlyDictionary<string, string>? keyToRepo,
        CancellationToken cancellationToken);
}
