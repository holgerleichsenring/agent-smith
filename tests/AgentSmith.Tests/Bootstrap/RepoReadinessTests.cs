using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Bootstrap;

/// <summary>
/// 2026-11-16-f3fba: a repository is ready when its base or its existing run branch carries the bootstrap,
/// incomplete when either says so, and unknown only when neither could answer.
/// </summary>
public sealed class RepoReadinessTests
{
    private const string Base = "main";
    private const string Run = "agent-smith/42";
    private readonly Mock<ISourceProvider> _provider = new();
    private readonly HashSet<string> _branches = [];
    private readonly Dictionary<(string Branch, string Path), string> _files = [];
    private readonly Dictionary<string, List<string>> _contexts = [];
    private static readonly RepoConnection Repo = new() { Name = "r", Url = "https://github.com/o/r", Type = RepoType.GitHub };

    public RepoReadinessTests()
    {
        _provider.Setup(p => p.BranchExistsAsync(It.IsAny<BranchName>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BranchName b, CancellationToken _) => _branches.Contains(b.Value));
        _provider.Setup(p => p.TryReadFileAsync(It.IsAny<string>(), It.IsAny<BranchName>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string path, BranchName b, CancellationToken _) => _files.GetValueOrDefault((b.Value, path)));
        _provider.Setup(p => p.ListDirectoryAsync(It.IsAny<string>(), It.IsAny<BranchName>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, BranchName b, CancellationToken _) => _contexts.GetValueOrDefault(b.Value) ?? []);
        _provider.Setup(p => p.ListDirectoryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
    }

    private void Branch(string branch, bool principles)
    {
        _branches.Add(branch);
        _contexts[branch] = ["api"];
        _files[(branch, ".agentsmith/contexts/api/context.yaml")] = "meta:\n  project: api\n  workdir: .\nstack:\n  lang: csharp\n";
        if (principles) _files[(branch, ".agentsmith/contexts/api/principles.md")] = "# principles";
    }

    private RepoReadiness Readiness()
    {
        var factory = new Mock<ISourceProviderFactory>();
        factory.Setup(f => f.Create(It.IsAny<RepoConnection>())).Returns(_provider.Object);
        var resolver = TestSandboxLanguageResolver.Create(factory.Object,
            new ContextYamlParser(new ContextYamlSerializer(new ContextYamlBuilders())));
        var probe = new RemoteBootstrapProbe(factory.Object, resolver, NullLogger<RemoteBootstrapProbe>.Instance);
        return new RepoReadiness(probe, factory.Object, NullLogger<RepoReadiness>.Instance);
    }

    private Task<RemoteBootstrapVerdict> ReadAsync() =>
        Readiness().ReadAsync(Repo, new BranchName(Base), new BranchName(Run), CancellationToken.None);

    [Fact]
    public async Task ReadAsync_BaseComplete_Ready()
    {
        Branch(Base, principles: true);

        (await ReadAsync()).State.Should().Be(RemoteBootstrapState.Complete);
    }

    [Fact]
    public async Task ReadAsync_BaseIncompleteRunBranchComplete_Ready()
    {
        Branch(Base, principles: false);
        Branch(Run, principles: true);

        (await ReadAsync()).State.Should().Be(RemoteBootstrapState.Complete,
            "an existing ticket branch gets its base merged at checkout, so either carrying it is enough");
    }

    [Fact]
    public async Task ReadAsync_BothIncomplete_Incomplete()
    {
        Branch(Base, principles: false);
        Branch(Run, principles: false);

        var verdict = await ReadAsync();

        verdict.State.Should().Be(RemoteBootstrapState.Incomplete);
        verdict.Missing.Should().ContainSingle().Which.File.Should().Be("principles.md");
    }

    [Fact]
    public async Task ReadAsync_BaseAbsentRunBranchIncomplete_Incomplete()
    {
        Branch(Run, principles: false);

        (await ReadAsync()).State.Should().Be(RemoteBootstrapState.Incomplete);
    }

    [Fact]
    public async Task ReadAsync_NeitherReadable_Unknown()
    {
        var verdict = await ReadAsync();

        verdict.State.Should().Be(RemoteBootstrapState.Unknown);
        verdict.Reason.Should().Contain("main");
    }

    [Fact]
    public async Task ReadAsync_RunBranchAbsent_IsNotRead()
    {
        Branch(Base, principles: false);

        (await ReadAsync()).State.Should().Be(RemoteBootstrapState.Incomplete);
        _provider.Verify(p => p.TryReadFileAsync(It.IsAny<string>(), new BranchName(Run), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SliceBaseAsync_RungNotPublishedYet_IsTheDefaultBranch()
    {
        var repo = new RepoConnection { Name = "r", Url = Repo.Url, Type = RepoType.GitHub, DefaultBranch = Base };

        (await Readiness().SliceBaseAsync(repo, new BranchName("agent-smith/7"), CancellationToken.None))
            .Should().Be(new BranchName(Base), "the first slice's checkout publishes the rung at the default branch");
    }

    [Fact]
    public async Task SliceBaseAsync_RungPublished_IsTheRung()
    {
        Branch("agent-smith/7", principles: true);

        (await Readiness().SliceBaseAsync(Repo, new BranchName("agent-smith/7"), CancellationToken.None))
            .Should().Be(new BranchName("agent-smith/7"));
    }
}
