using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Demo;

/// <summary>
/// p0326: extracts the embedded demo sample project (seeded bug + failing
/// boundary test) into a workspace directory and git-inits it with one
/// baseline commit. The result is a fully local repo the fix-bug preset can
/// run against — RepoType.Local, no remote, no tracker.
/// </summary>
public sealed class DemoWorkspaceMaterializer(
    IEmbeddedDemoSample sample,
    ICatalogTarballExtractor extractor,
    IDemoGitInitializer gitInitializer,
    ILogger<DemoWorkspaceMaterializer> logger)
{
    /// <summary>Materializes into <paramref name="targetDir"/> (replaced if present)
    /// and returns it as an absolute path.</summary>
    public async Task<string> MaterializeAsync(string targetDir, CancellationToken cancellationToken)
    {
        var workspace = Path.GetFullPath(targetDir);
        await using (var stream = sample.Open())
        {
            extractor.Extract(stream, workspace);
        }
        await gitInitializer.InitializeAsync(workspace, cancellationToken);
        logger.LogInformation("Demo workspace materialized at {Workspace}", workspace);
        return workspace;
    }
}
