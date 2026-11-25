using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Sandbox;

/// <summary>
/// 2026-11-25-9992b: a run's workspace in-process — ONE owned temp directory as /work, every
/// <see cref="WorkspaceSpec.HostSources"/> entry served at /work/&lt;repo&gt; (never deleted), and one sandbox that
/// runs every image's steps on the host.
/// </summary>
public sealed class InProcessWorkspaceFactory(
    InProcessStepEnvironmentReader environment, ILoggerFactory loggerFactory) : IWorkspaceFactory
{
    public Task<IWorkspace> CreateAsync(WorkspaceSpec spec, CancellationToken cancellationToken)
    {
        var jobId = Guid.NewGuid().ToString("N");
        var root = Path.Combine(Path.GetTempPath(), $"agentsmith-{jobId[..12]}");
        Directory.CreateDirectory(root);
        var mounts = (spec.HostSources ?? new Dictionary<string, string>())
            .Where(source => Directory.Exists(source.Value))
            .Select(source => new InProcessRepoMount(source.Key, source.Value))
            .ToList();
        var sandbox = new InProcessSandbox(jobId, root, ownsWorkDir: true, environment.Read(),
            loggerFactory.CreateLogger<InProcessSandbox>(), mounts);
        return Task.FromResult<IWorkspace>(new InProcessWorkspace([.. spec.Toolchains.Select(t => t.Image)], sandbox));
    }
}
