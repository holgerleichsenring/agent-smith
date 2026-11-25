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
/// 2026-11-25-9992c; 2026-11-25-9992b: a run repository lives at /work/&lt;repo&gt;, so the in-process workspace
/// serves every host source it is handed there — /work itself is a fresh directory of its own, any image is served
/// by the one host sandbox, and every source survives the workspace.
/// </summary>
[Trait("TestProcess", "serial")]
[Collection(ExternalProcessCollection.Name)]
public sealed class InProcessWorkspaceTests : IDisposable
{
    private readonly string _source = Path.Combine(Path.GetTempPath(), $"agentsmith-src-mount-{Guid.NewGuid():N}");

    public InProcessWorkspaceTests() => Directory.CreateDirectory(_source);

    public void Dispose()
    {
        _workspace?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        if (Directory.Exists(_source)) Directory.Delete(_source, recursive: true);
        if (Directory.Exists(_other)) Directory.Delete(_other, recursive: true);
    }

    private readonly string _other = Path.Combine(Path.GetTempPath(), $"agentsmith-src-other-{Guid.NewGuid():N}");
    private IWorkspace? _workspace;

    private async Task<IWorkspace> WorkspaceAsync()
    {
        Directory.CreateDirectory(_other);
        var spec = new WorkspaceSpec(new SandboxSpec("dotnet:9", ResourceLimits.Default),
            [new WorkspaceToolchain("dotnet:9", ResourceLimits.Default), new WorkspaceToolchain("node:20", ResourceLimits.Default)],
            new Dictionary<string, string> { ["api"] = _source, ["web"] = _other });
        return _workspace = await new InProcessWorkspaceFactory(InProcessStepEnvironmentReaders.Empty(), NullLoggerFactory.Instance)
            .CreateAsync(spec, CancellationToken.None);
    }

    private async Task<ISandbox> RepoAsync(string repo = "api", string image = "dotnet:9") =>
        new RepoSandbox((_workspace ?? await WorkspaceAsync()).Container(image), repo);

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
    public async Task DisposeAsync_MountedSources_AreNeverDeleted()
    {
        var workspace = await WorkspaceAsync();
        _workspace = null;

        await workspace.DisposeAsync();

        Directory.Exists(_source).Should().BeTrue();
        Directory.Exists(_other).Should().BeTrue();
    }

    [Fact]
    public async Task Container_AnyImage_IsTheOneHostSandbox()
    {
        var workspace = await WorkspaceAsync();

        workspace.Serves("python:3.12").Should().BeTrue("in-process every step runs on the host");
        workspace.Container("python:3.12").Should().BeSameAs(workspace.Container("dotnet:9"));
        workspace.Images.Should().Equal("dotnet:9", "node:20");
    }

    [Fact]
    public async Task WriteFile_UnderASecondRepository_LandsInItsOwnSource()
    {
        var sandbox = await RepoAsync("web", "node:20");

        var result = await sandbox.RunStepAsync(new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.WriteFile,
            Path: "x.txt", Content: "second"), null, CancellationToken.None);

        result.ExitCode.Should().Be(0);
        (await File.ReadAllTextAsync(Path.Combine(_other, "x.txt"))).Should().Be("second");
        File.Exists(Path.Combine(_source, "x.txt")).Should().BeFalse();
    }
}
