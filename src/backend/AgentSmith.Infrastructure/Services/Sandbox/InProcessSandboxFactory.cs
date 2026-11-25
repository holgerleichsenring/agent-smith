using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Sandbox;

public sealed class InProcessSandboxFactory(
    InProcessStepEnvironmentReader environment, ILoggerFactory loggerFactory) : ISandboxFactory
{
    public Task<ISandbox> CreateAsync(SandboxSpec spec, CancellationToken cancellationToken)
    {
        var jobId = Guid.NewGuid().ToString("N");
        // When the pipeline already cloned the source host-side (api-security-scan path
        // via TryCheckoutSourceHandler), reuse that directory as workDir so handlers
        // reading from /work see the cloned files. Otherwise fall back to a fresh
        // empty temp dir (legacy behaviour for pipelines that produce content in-sandbox).
        //
        // ownsWorkDir tracks who is responsible for cleanup: we own (and must delete on
        // dispose) only the fallback temp dir we created. Reused InitialSourcePath is
        // the operator's working tree — DisposeAsync must never recursive-delete that.
        //
        // 2026-11-25-9992c: a run repository (spec.RepoName) lives at /work/<repo>, so its reused source is
        // MOUNTED there and /work is a fresh owned dir; without a name the source still IS /work.
        var reuseSource = !string.IsNullOrEmpty(spec.InitialSourcePath) && Directory.Exists(spec.InitialSourcePath);
        var mount = reuseSource && !string.IsNullOrEmpty(spec.RepoName)
            ? new InProcessRepoMount(spec.RepoName!, spec.InitialSourcePath!)
            : null;
        var workDir = reuseSource && mount is null
            ? spec.InitialSourcePath!
            : Path.Combine(Path.GetTempPath(), $"agentsmith-{jobId[..12]}");
        Directory.CreateDirectory(workDir);
        var sandbox = new InProcessSandbox(jobId, workDir, ownsWorkDir: workDir != spec.InitialSourcePath,
            environment.Read(), loggerFactory.CreateLogger<InProcessSandbox>(), mount);
        return Task.FromResult<ISandbox>(sandbox);
    }
}
