using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Sandbox;

public sealed class InProcessSandboxFactory(ILoggerFactory loggerFactory) : ISandboxFactory
{
    public Task<ISandbox> CreateAsync(SandboxSpec spec, CancellationToken cancellationToken)
    {
        var jobId = Guid.NewGuid().ToString("N");
        var workDir = Path.Combine(Path.GetTempPath(), $"agentsmith-{jobId[..12]}");
        Directory.CreateDirectory(workDir);
        var sandbox = new InProcessSandbox(jobId, workDir, loggerFactory.CreateLogger<InProcessSandbox>());
        return Task.FromResult<ISandbox>(sandbox);
    }
}
