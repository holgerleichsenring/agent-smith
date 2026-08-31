using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Infrastructure.Services.Sandbox;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

using AgentSmith.Tests.Architecture;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// p0419: registry staging reads the repo's own package configs out of the sandbox.
/// Run 354b found the file and staged nothing, because a read that comes back empty
/// is skipped without a word — so the private feed stayed unauthenticated and every
/// build went red.
/// </summary>
[Collection(ExternalProcessCollection.Name)]
public sealed class RegistryConfigDiscoveryTests(ITestOutputHelper output)
{
    [Fact]
    public async Task AConfigAtTheRepoRoot_IsListedAndReadable()
    {
        var workDir = Directory.CreateTempSubdirectory("agentsmith-p0419-reg-").FullName;
        try
        {
            await File.WriteAllTextAsync(Path.Combine(workDir, "nuget.config"),
                """
                <?xml version="1.0" encoding="utf-8"?>
                <configuration>
                  <packageSources>
                    <add key="Private.Feed" value="https://pkgs.dev.azure.com/org/_packaging/f/nuget/v3/index.json" />
                  </packageSources>
                </configuration>
                """);

            var sandbox = new InProcessSandbox(
                jobId: "p0419", workDir, ownsWorkDir: false,
                NullLogger<InProcessSandbox>.Instance);
            var reader = new SandboxFileReaderFactory().Create(sandbox);

            var listing = await reader.ListAsync("/work", maxDepth: 6, CancellationToken.None);
            output.WriteLine("listing: " + string.Join(" | ", listing.Take(10)));

            var found = listing.Where(p =>
                p.EndsWith("/nuget.config", StringComparison.OrdinalIgnoreCase)).ToList();
            found.Should().ContainSingle("the discovery filter has to see a config at the repo root");

            var content = await reader.TryReadAsync(found[0], CancellationToken.None);
            output.WriteLine("content: " + (content ?? "<null>"));
            content.Should().NotBeNullOrEmpty(
                "a listed path must be readable — a silent empty read is what let an "
                + "authenticated feed stay unauthenticated for a whole run");
        }
        finally
        {
            try { Directory.Delete(workDir, recursive: true); } catch (IOException) { }
        }
    }

    /// <summary>
    /// p0419: "/root" is the canonical sandbox home the way "/work" is the canonical
    /// working tree. In-process it was not translated at all, so staging the matched
    /// feed credential died with "Read-only file system : /root" — the credential was
    /// correct, the feed was matched, and the write had nowhere to land.
    /// </summary>
    [Fact]
    public async Task ACredentialStagedAtTheCanonicalHome_LandsWhereTheToolchainLooks()
    {
        var workDir = Directory.CreateTempSubdirectory("agentsmith-p0419-home-").FullName;
        try
        {
            var sandbox = new InProcessSandbox(
                jobId: "p0419home", workDir, ownsWorkDir: false,
                NullLogger<InProcessSandbox>.Instance);
            var reader = new SandboxFileReaderFactory().Create(sandbox);

            await reader.WriteAsync("/root/.nuget/NuGet/NuGet.Config", "<configuration/>",
                CancellationToken.None);
            var readBack = await reader.TryReadAsync(
                "/root/.nuget/NuGet/NuGet.Config", CancellationToken.None);

            readBack.Should().Contain("configuration",
                "a canonical path that only half exists is worse than none");

            var step = new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
                Command: "/bin/sh", Args: ["-c", "echo $HOME"], TimeoutSeconds: 30);
            var lines = new List<string>();
            var result = await sandbox.RunStepAsync(step,
                new SyncProgress<StepEvent>(ev =>
                {
                    if (ev.Kind == StepEventKind.Stdout) lines.Add(ev.Line);
                }),
                CancellationToken.None);

            result.ExitCode.Should().Be(0);
            lines.Should().NotBeEmpty();
            Directory.Exists(lines[0]).Should().BeTrue(
                "the toolchain has to read the credential that was staged for it");
        }
        finally
        {
            try { Directory.Delete(workDir, recursive: true); } catch (IOException) { }
        }
    }
}
