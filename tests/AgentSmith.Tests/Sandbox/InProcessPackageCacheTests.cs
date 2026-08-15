using AgentSmith.Contracts.Sandbox;
using AgentSmith.Infrastructure.Services.Sandbox;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// p0419: the in-process sandbox gives a command the SAME caches the container backend
/// mounts — every row of the catalog, no ecosystem privileged.
/// <para>
/// The first cut set NUGET_PACKAGES and nothing else. That is a hole, not a fix: npm,
/// pip, go and cargo would each have gone cold behind the sandbox's private home, and
/// the next ecosystem would have needed another line here instead of one catalog row.
/// </para>
/// </summary>
public sealed class InProcessPackageCacheTests
{
    [Fact]
    public async Task EveryEcosystemInTheCatalog_IsPointedAtTheSharedCache()
    {
        var workDir = Directory.CreateTempSubdirectory("agentsmith-p0419-cache-").FullName;
        try
        {
            var sandbox = new InProcessSandbox(
                jobId: "p0419cache", workDir, ownsWorkDir: false,
                NullLogger<InProcessSandbox>.Instance);

            var seen = new List<string>();
            var step = new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
                Command: "/bin/sh", Args: ["-c", "env"], TimeoutSeconds: 30);
            await sandbox.RunStepAsync(step,
                new AgentSmith.Application.Services.Sandbox.SyncProgress<StepEvent>(ev =>
                {
                    if (ev.Kind == StepEventKind.Stdout) seen.Add(ev.Line);
                }),
                CancellationToken.None);

            var expected = PackageCacheCatalog.All.SelectMany(m => m.Env.Keys).ToList();
            expected.Should().HaveCountGreaterThan(1, "the catalog covers several ecosystems");

            foreach (var name in expected)
            {
                var line = seen.FirstOrDefault(l => l.StartsWith(name + "=", StringComparison.Ordinal));
                line.Should().NotBeNull(
                    $"{name} comes from the catalog — a sandbox that sets only one "
                    + "ecosystem's cache leaves every other one cold");
                line!.Split('=', 2)[1].Should().StartWith(SandboxPathMap.CacheDir,
                    "a cache that moves with the sandbox is not a cache");
            }
        }
        finally
        {
            try { Directory.Delete(workDir, recursive: true); } catch (IOException) { }
        }
    }
}
