using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Builders;
using AgentSmith.Application.Services.Pocs.Commit;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Infrastructure.Services.Sandbox;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Tests.Integration;
using AgentSmith.Tests.Sandbox;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Pocs.Commit;

/// <summary>
/// 2026-11-23-933dc: a shared source repo for PoC commits — a real bare origin on 'main' seeded with a root README and
/// another person's PoC folder — and a <see cref="PocFolderWrite"/> whose sandboxes are real in-process ones, each
/// optionally wrapped (to push a concurrent commit or refuse a push). Null without git.
/// </summary>
internal sealed class PocGitRig : IAsyncDisposable
{
    internal const string Folder = "pocs/poc-ann/todo";
    internal const string OtherFolder = "pocs/poc-bob/shop";
    private readonly string _root = Path.Combine(Path.GetTempPath(), "as-933dc-git-" + Guid.NewGuid().ToString("N"));

    internal string Origin => Path.Combine(_root, "origin.git");

    internal List<string> Sandboxes { get; } = [];

    internal Func<ISandbox, ISandbox>? Wrap { get; set; }

    internal RepoConnection Repo => new() { Name = "poc-src", Type = RepoType.GitHub, Url = Origin, DefaultBranch = "main" };

    internal static async Task<PocGitRig?> CreateAsync()
    {
        if (!SandboxToolAvailability.IsAvailable("git")) return null;
        var rig = new PocGitRig();
        var seed = Path.Combine(rig._root, "seed");
        Directory.CreateDirectory(Path.Combine(seed, OtherFolder));
        await File.WriteAllTextAsync(Path.Combine(seed, "README.md"), "root\n");
        await File.WriteAllTextAsync(Path.Combine(seed, OtherFolder, "app.py"), "print('bob')\n");
        await rig.GitAsync(seed, "init", "-q", "-b", "main");
        await rig.CommitAllAsync(seed, "seed");
        await rig.GitAsync(rig._root, "clone", "-q", "--bare", seed, rig.Origin);
        return rig;
    }

    internal PocFolderWrite Writer()
    {
        var factory = new Mock<ISandboxFactory>();
        factory.Setup(f => f.CreateAsync(It.IsAny<SandboxSpec>(), It.IsAny<CancellationToken>())).ReturnsAsync(() =>
        {
            var dir = Path.Combine(_root, "sandbox-" + Sandboxes.Count);
            Directory.CreateDirectory(dir);
            Sandboxes.Add(dir);
            ISandbox sandbox = new InProcessSandbox("poc", dir, ownsWorkDir: false, InProcessStepEnvironment.Inherit, NullLogger.Instance);
            return Wrap?.Invoke(sandbox) ?? sandbox;
        });
        var files = new SandboxFileReaderFactory();
        var git = new PocGit(TestGitCredentials.Resolver);
        var operations = SeriesDoubles.Git(files);
        return new PocFolderWrite(factory.Object,
            new SandboxSpecBuilder(new StubSandboxResourceResolver(), Mock.Of<IAgentImageResolver>(r => r.Resolve(It.IsAny<ResolvedProject>()) == "agent:test")),
            Mock.Of<IRunContextAccessor>(r => r.CurrentRunId == "run-1"), new PocSparseCheckout(git, TestGitCredentials.Resolver),
            new PocFolderReplace(git, new ReferenceSetMaterialiser(Mock.Of<IReferenceSetReader>(), files, new SandboxBinaryFileWriter())),
            new PocFolderPush(operations, new GitBranchPusher(), git, TestGitCredentials.Resolver), operations);
    }

    /// <summary>A commit to <see cref="OtherFolder"/> pushed to the origin from a clone of its own.</summary>
    internal async Task PushOtherFolderAsync(string text)
    {
        var clone = Path.Combine(_root, "other-" + Guid.NewGuid().ToString("N")[..8]);
        await GitAsync(_root, "clone", "-q", Origin, clone);
        await File.WriteAllTextAsync(Path.Combine(clone, OtherFolder, "app.py"), text);
        await CommitAllAsync(clone, "bob's change");
        await GitAsync(clone, "push", "-q", "origin", "main");
    }

    internal Task<string> OriginAsync(params string[] args) => GitAsync(_root, ["--git-dir", Origin, .. args]);

    internal async Task CommitAllAsync(string dir, string message)
    {
        await GitAsync(dir, "-c", "user.email=t@example.test", "-c", "user.name=t", "add", "-A");
        await GitAsync(dir, "-c", "user.email=t@example.test", "-c", "user.name=t", "commit", "-q", "-m", message);
    }

    internal async Task<string> GitAsync(string dir, params string[] args)
    {
        await using var sandbox = new InProcessSandbox("git", _root, ownsWorkDir: false, InProcessStepEnvironment.Inherit, NullLogger.Instance);
        var result = await sandbox.RunStepAsync(new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: "git", Args: ["-C", dir, .. args], TimeoutSeconds: 60), null, CancellationToken.None);
        result.ExitCode.Should().Be(0, $"git {string.Join(' ', args)}: {result.ErrorMessage}");
        return (result.OutputContent ?? string.Empty).Trim();
    }

    public ValueTask DisposeAsync()
    {
        try { Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        return ValueTask.CompletedTask;
    }
}
