using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Sandbox;

public sealed class InProcessSandboxFactory(ILoggerFactory loggerFactory) : ISandboxFactory
{
    public Task<ISandbox> CreateAsync(SandboxSpec spec, CancellationToken cancellationToken)
    {
        var jobId = Guid.NewGuid().ToString("N");
        // When the pipeline already cloned the source host-side (api-security-scan path
        // via TryCheckoutSourceHandler), reuse that directory as workDir so handlers
        // reading from /work see the cloned files. Otherwise fall back to a fresh
        // empty temp dir (legacy behaviour for pipelines that produce content in-sandbox).
        var workDir = !string.IsNullOrEmpty(spec.InitialSourcePath) && Directory.Exists(spec.InitialSourcePath)
            ? spec.InitialSourcePath
            : Path.Combine(Path.GetTempPath(), $"agentsmith-{jobId[..12]}");
        Directory.CreateDirectory(workDir);
        var sandbox = new InProcessSandbox(jobId, workDir, loggerFactory.CreateLogger<InProcessSandbox>());
        return Task.FromResult<ISandbox>(sandbox);
    }
}
