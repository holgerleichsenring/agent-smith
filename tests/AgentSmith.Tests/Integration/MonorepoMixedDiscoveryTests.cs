using System.Runtime.CompilerServices;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Infrastructure.Services;
using AgentSmith.Infrastructure.Services.Providers.Source;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Integration;

/// <summary>
/// p0161b: end-to-end discovery against the on-disk monorepo-mixed fixture
/// (tests/AgentSmith.Tests.Fixtures/monorepo-mixed/). Uses the real
/// LocalSourceProvider + real ContextYamlParser — no mocks below the
/// SandboxLanguageResolver layer — so the full file-IO + YAML parsing path
/// is exercised, not just the unit-mocked surface in
/// SandboxLanguageResolverTests.
/// </summary>
public sealed class MonorepoMixedDiscoveryTests
{
    private static readonly string FixturePath = LocateFixture();

    [Fact]
    public async Task ResolveAllAsync_MonorepoFixture_ReturnsThreeContexts_WithPerContextLanguage()
    {
        var sut = NewResolver(FixturePath);
        var repo = new RepoConnection
        {
            Name = "monorepo-mixed",
            Type = RepoType.Local,
            Path = FixturePath,
            Url = $"file://{FixturePath}"
        };

        var result = await sut.ResolveAllAsync(repo, CancellationToken.None);

        result.Should().HaveCount(3);
        result.Should().ContainSingle(d => d.ContextName == "server")
            .Which.Should().BeEquivalentTo(new { Workdir = "src/Server", Language = "csharp" });
        result.Should().ContainSingle(d => d.ContextName == "client")
            .Which.Should().BeEquivalentTo(new { Workdir = "src/Client", Language = "typescript" });
        result.Should().ContainSingle(d => d.ContextName == "docs")
            .Which.Should().BeEquivalentTo(new { Workdir = "docs", Language = "markdown" });
    }

    [Fact]
    public async Task ResolveAllAsync_MonorepoFixture_ContextNamesMatchSandboxKeyComposerOutput()
    {
        var sut = NewResolver(FixturePath);
        var repo = new RepoConnection
        {
            Name = "monorepo-mixed",
            Type = RepoType.Local,
            Path = FixturePath,
            Url = $"file://{FixturePath}"
        };

        var discoveries = await sut.ResolveAllAsync(repo, CancellationToken.None);

        // Single-repo monorepo → bare context-name per SandboxKeyComposer.
        var keys = discoveries
            .Select(d => SandboxKeyComposer.Compose(
                repoCount: 1, repoName: repo.Name,
                perRepoDiscoveryCount: discoveries.Count, contextName: d.ContextName))
            .ToList();
        keys.Should().BeEquivalentTo(new[] { "server", "client", "docs" });
    }

    private static SandboxLanguageResolver NewResolver(string fixturePath)
    {
        var factory = new Mock<ISourceProviderFactory>();
        factory.Setup(f => f.Create(It.IsAny<RepoConnection>()))
            .Returns(new LocalSourceProvider(fixturePath));
        return new SandboxLanguageResolver(
            factory.Object,
            new ContextYamlParser(new ContextYamlSerializer()),
            NullLogger<SandboxLanguageResolver>.Instance);
    }

    private static string LocateFixture([CallerFilePath] string callerPath = "")
    {
        // This file lives at tests/AgentSmith.Tests/Integration/MonorepoMixedDiscoveryTests.cs
        // → up three levels reaches the repo root.
        var repoRoot = Directory.GetParent(callerPath)!.Parent!.Parent!.Parent!.FullName;
        var fixture = Path.Combine(repoRoot, "tests", "AgentSmith.Tests.Fixtures", "monorepo-mixed");
        if (!Directory.Exists(fixture))
            throw new DirectoryNotFoundException("p0161b fixture not found at " + fixture);
        return fixture;
    }
}
