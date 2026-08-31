using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// p0161a: SandboxLanguageResolver.ResolveAllAsync discovers contexts on a
/// remote repo via ISourceProvider (pre-sandbox). Each
/// .agentsmith/contexts/&lt;name&gt;/context.yaml produces one
/// RemoteContextDiscovery; empty discovery → one synthetic
/// ("default", ".", null).
/// </summary>
public sealed class SandboxLanguageResolverTests
{
    private readonly Mock<ISourceProviderFactory> _sourceFactoryMock = new();
    private readonly Mock<ISourceProvider> _sourceProviderMock = new();
    private readonly Mock<IContextYamlParser> _parserMock = new();
    private readonly RepoConnection _source = new() { Url = "https://example.com/repo.git" };
    private readonly SandboxLanguageResolver _sut;

    public SandboxLanguageResolverTests()
    {
        _sourceFactoryMock.Setup(f => f.Create(It.IsAny<RepoConnection>())).Returns(_sourceProviderMock.Object);
        _sut = new SandboxLanguageResolver(
            _sourceFactoryMock.Object,
            _parserMock.Object,
            NullLogger<SandboxLanguageResolver>.Instance);
    }

    [Fact]
    public async Task ResolveAllAsync_RemoteFanout_ReturnsPerContext()
    {
        _sourceProviderMock.Setup(p => p.ListDirectoryAsync(".agentsmith/contexts", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "server", "client", "docs" });
        SetupContextYaml("server", "src/Server", "csharp");
        SetupContextYaml("client", "src/Client", "typescript");
        SetupContextYaml("docs", "docs", "markdown");

        var result = await _sut.ResolveAllAsync(_source, CancellationToken.None);

        result.Should().BeEquivalentTo(new[]
        {
            new RemoteContextDiscovery("server", "src/Server", "csharp"),
            new RemoteContextDiscovery("client", "src/Client", "typescript"),
            new RemoteContextDiscovery("docs", "docs", "markdown"),
        });
    }

    [Fact]
    public async Task ResolveAllAsync_CarriesPrerequisitesFromContextYaml()
    {
        // p0202a: prerequisites is read here (with language) and carried
        // on the discovery so it reaches the early EnsurePrerequisites step.
        _sourceProviderMock.Setup(p => p.ListDirectoryAsync(".agentsmith/contexts", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "node" });
        var yaml = "yaml-content-node";
        _sourceProviderMock.Setup(p => p.TryReadFileAsync(
                ".agentsmith/contexts/node/context.yaml", It.IsAny<CancellationToken>()))
            .ReturnsAsync(yaml);
        _parserMock.Setup(p => p.Parse(yaml))
            .Returns(ContextYamlParseResult.Ok(new ContextYamlSummary("frontend", "node", "npm ci")));

        var result = await _sut.ResolveAllAsync(_source, CancellationToken.None);

        result.Should().ContainSingle()
            .Which.Should().Be(new RemoteContextDiscovery("node", "frontend", "node", "npm ci"));
    }

    [Fact]
    public async Task ResolveAllAsync_NoContextsDir_FallbackDefault()
    {
        _sourceProviderMock.Setup(p => p.ListDirectoryAsync(".agentsmith/contexts", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<string>());

        var result = await _sut.ResolveAllAsync(_source, CancellationToken.None);

        result.Should().ContainSingle()
            .Which.Should().Be(new RemoteContextDiscovery("default", ".", null));
    }

    [Fact]
    public async Task ResolveAllAsync_RemoteThrows_FallbackDefault()
    {
        _sourceProviderMock.Setup(p => p.ListDirectoryAsync(".agentsmith/contexts", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("connection refused"));

        var result = await _sut.ResolveAllAsync(_source, CancellationToken.None);

        result.Should().ContainSingle()
            .Which.Should().Be(new RemoteContextDiscovery("default", ".", null));
    }

    [Fact]
    public async Task ResolveAllAsync_AllSubDirsFailToParse_FallbackDefault()
    {
        _sourceProviderMock.Setup(p => p.ListDirectoryAsync(".agentsmith/contexts", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "garbage" });
        _sourceProviderMock.Setup(p => p.TryReadFileAsync(".agentsmith/contexts/garbage/context.yaml", It.IsAny<CancellationToken>()))
            .ReturnsAsync("not-yaml");
        _parserMock.Setup(p => p.Parse("not-yaml"))
            .Returns(ContextYamlParseResult.Error("(Line: 1, Col: 1): mock parse failure"));

        var result = await _sut.ResolveAllAsync(_source, CancellationToken.None);

        result.Should().ContainSingle()
            .Which.Should().Be(new RemoteContextDiscovery("default", ".", null));
    }

    [Fact]
    public async Task ResolveAllAsync_EmptyUrl_FallbackDefault()
    {
        var emptyRepo = new RepoConnection { Url = null };

        var result = await _sut.ResolveAllAsync(emptyRepo, CancellationToken.None);

        result.Should().ContainSingle()
            .Which.Should().Be(new RemoteContextDiscovery("default", ".", null));
    }

    [Fact]
    public async Task ResolveContextAsync_NamedContext_ReadsThatContextYaml()
    {
        // p0261: `--context api` pins to exactly that context, reading its toolchain.
        SetupContextYaml("api", "src/Api", "csharp");

        var result = await _sut.ResolveContextAsync(_source, "api", CancellationToken.None);

        result.Should().ContainSingle()
            .Which.Should().Be(new RemoteContextDiscovery("api", "src/Api", "csharp"));
    }

    [Fact]
    public async Task ResolveContextAsync_Unreadable_NamedSyntheticNotDefault()
    {
        // Falls back to a NAMED synthetic so the probe hits contexts/api/, never
        // the misleading "default".
        _sourceProviderMock.Setup(p => p.TryReadFileAsync(
                ".agentsmith/contexts/api/context.yaml", It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var result = await _sut.ResolveContextAsync(_source, "api", CancellationToken.None);

        result.Should().ContainSingle()
            .Which.Should().Be(new RemoteContextDiscovery("api", ".", null));
    }

    [Fact]
    public async Task ResolveContextAsync_LocalSourceEmptyUrl_StillResolvesNamedContext()
    {
        // The exact bug the flag fixes: ResolveAllAsync short-circuits to "default"
        // when Url is empty (a local --source-path checkout), never discovering the
        // real contexts. The override path must NOT short-circuit — it reads the
        // named context directly so a local monorepo can target one of its contexts.
        var localSource = new RepoConnection { Url = null };
        SetupContextYaml("api", "src/Api", "csharp");

        var result = await _sut.ResolveContextAsync(localSource, "api", CancellationToken.None);

        result.Should().ContainSingle()
            .Which.Should().Be(new RemoteContextDiscovery("api", "src/Api", "csharp"));
    }

    private void SetupContextYaml(string contextName, string workdir, string language)
    {
        var yaml = $"yaml-content-{contextName}";
        _sourceProviderMock.Setup(p => p.TryReadFileAsync(
                $".agentsmith/contexts/{contextName}/context.yaml", It.IsAny<CancellationToken>()))
            .ReturnsAsync(yaml);
        _parserMock.Setup(p => p.Parse(yaml))
            .Returns(ContextYamlParseResult.Ok(new ContextYamlSummary(workdir, language)));
    }
}
