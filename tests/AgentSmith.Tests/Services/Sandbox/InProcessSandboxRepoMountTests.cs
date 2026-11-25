using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Infrastructure.Services.Sandbox;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Tests.Architecture;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services.Sandbox;

/// <summary>
/// 2026-11-25-9992c: a run repository lives at /work/&lt;repo&gt;, so an in-process sandbox handed a host-cloned
/// source AND the repository's name serves that source there — /work itself is a fresh directory of its own,
/// and the source survives the sandbox.
/// </summary>
[Trait("TestProcess", "serial")]
[Collection(ExternalProcessCollection.Name)]
public sealed class InProcessSandboxRepoMountTests : IDisposable
{
    private readonly string _source = Path.Combine(Path.GetTempPath(), $"agentsmith-src-mount-{Guid.NewGuid():N}");

    public InProcessSandboxRepoMountTests() => Directory.CreateDirectory(_source);

    public void Dispose()
    {
        if (Directory.Exists(_source)) Directory.Delete(_source, recursive: true);
    }

    private async Task<ISandbox> RepoAsync() =>
        new RepoSandbox(await new InProcessSandboxFactory(InProcessStepEnvironmentReaders.Empty(), NullLoggerFactory.Instance)
            .CreateAsync(new SandboxSpec("irrelevant", ResourceLimits.Default, InitialSourcePath: _source, RepoName: "api"),
                CancellationToken.None), "api");

    private static Step Shell(string script, string? workingDirectory = null) =>
        new(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run, Command: "/bin/sh", Args: ["-c", script],
            WorkingDirectory: workingDirectory);

    [Fact]
    public async Task WriteFile_UnderTheRepositoryRoot_LandsInTheSourceDirectory()
    {
        await using var sandbox = await RepoAsync();

        var result = await sandbox.RunStepAsync(new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.WriteFile,
            Path: "/work/api/x.txt", Content: "mounted"), null, CancellationToken.None);

        result.ExitCode.Should().Be(0);
        (await File.ReadAllTextAsync(Path.Combine(_source, "x.txt"))).Should().Be("mounted");
    }

    [Fact]
    public async Task RunStep_AtTheRepositoryRoot_ExecutesInTheSourceDirectory()
    {
        await using var sandbox = await RepoAsync();

        var explicitRoot = await sandbox.RunStepAsync(Shell("echo one > one.txt", "/work/api"), null, CancellationToken.None);
        var defaultRoot = await sandbox.RunStepAsync(Shell("echo two > two.txt"), null, CancellationToken.None);

        explicitRoot.ExitCode.Should().Be(0);
        defaultRoot.ExitCode.Should().Be(0);
        File.Exists(Path.Combine(_source, "one.txt")).Should().BeTrue();
        File.Exists(Path.Combine(_source, "two.txt")).Should().BeTrue();
    }

    [Fact]
    public async Task ListFiles_UnderTheRepositoryRoot_NamesTheSourceFilesAtTheirSandboxPaths()
    {
        await File.WriteAllTextAsync(Path.Combine(_source, "listed.txt"), "x");
        await using var sandbox = await RepoAsync();

        var listing = await new SandboxFileReaderFactory().Create(sandbox).ListAsync(".", 1, CancellationToken.None);

        listing.Should().Contain("/work/api/listed.txt");
    }

    [Fact]
    public async Task IsBindMounted_SourceIsAWorkTree_AnswersTrueAtTheRepositoryRoot()
    {
        await using var sandbox = await RepoAsync();
        (await sandbox.RunStepAsync(new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run, Command: "git",
            Args: ["init", "-q"]), null, CancellationToken.None)).ExitCode.Should().Be(0);

        var mounted = await LocalRepoCheckout.IsBindMountedAsync([new("api", sandbox)], CancellationToken.None);

        mounted.Should().BeTrue();
    }

    [Fact]
    public async Task DisposeAsync_MountedSource_IsNeverDeleted()
    {
        var sandbox = await RepoAsync();

        await sandbox.DisposeAsync();

        Directory.Exists(_source).Should().BeTrue();
    }
}
