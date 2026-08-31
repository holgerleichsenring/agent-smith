using AgentSmith.Contracts.Commands;

namespace AgentSmith.Contracts.Sandbox;

/// <summary>
/// p0356: probes what each sandbox's toolchain image actually provides (shell,
/// language SDK versions, git) with a single command at master start, and
/// distills the result into a capability section for the master context — the
/// master KNOWS what it can run (scripts, codemods, compilers) instead of
/// guessing from the repo language. Returns null when nothing could be probed
/// (the section is simply absent — never a fabricated inventory).
/// <para>
/// 2026-08-31-7097: the run's context comes with it, because the same sweep looks for
/// the binaries the repository's declared verify stages name and reports the ones the
/// image does not carry.
/// </para>
/// </summary>
public interface ISandboxToolchainProbe
{
    Task<string?> ProbeAsync(
        PipelineContext pipeline,
        IReadOnlyDictionary<string, ISandbox> sandboxes,
        IReadOnlyDictionary<string, string>? keyToRepo,
        CancellationToken cancellationToken);
}
