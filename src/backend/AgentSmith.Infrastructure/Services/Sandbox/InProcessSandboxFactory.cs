using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Sandbox;

/// <summary>
/// A single in-process sandbox over a fresh temp directory of its own, deleted with it. 2026-11-25-9992b: a host
/// source the run already holds is served by <see cref="InProcessWorkspaceFactory"/> at /work/&lt;repo&gt;; a
/// single sandbox no longer reuses one as its /work.
/// </summary>
public sealed class InProcessSandboxFactory(
    InProcessStepEnvironmentReader environment, ILoggerFactory loggerFactory) : ISandboxFactory
{
    public Task<ISandbox> CreateAsync(SandboxSpec spec, CancellationToken cancellationToken)
    {
        var jobId = Guid.NewGuid().ToString("N");
        var workDir = Path.Combine(Path.GetTempPath(), $"agentsmith-{jobId[..12]}");
        Directory.CreateDirectory(workDir);
        var sandbox = new InProcessSandbox(jobId, workDir, ownsWorkDir: true,
            environment.Read(), loggerFactory.CreateLogger<InProcessSandbox>());
        return Task.FromResult<ISandbox>(sandbox);
    }
}
