using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Text.Json;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// p0135: SandboxLanguageResolver picks the project's primary language from a
/// host-side project-map.json cache first, then from a remote
/// .agentsmith/context.yaml read via the source provider, returning
/// (null, GenericFallback) when both layers miss.
/// </summary>
public sealed class SandboxLanguageResolverTests : IDisposable
{
    private readonly string _tempCacheRoot = Path.Combine(
        Path.GetTempPath(), "agentsmith-test-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly Mock<IAgentSmithPaths> _pathsMock = new();
    private readonly Mock<ISourceProviderFactory> _sourceFactoryMock = new();
    private readonly Mock<ISourceProvider> _sourceProviderMock = new();
    private readonly SourceConfig _source = new() { Url = "https://example.com/repo.git" };
    private readonly SandboxLanguageResolver _sut;

    public SandboxLanguageResolverTests()
    {
        Directory.CreateDirectory(_tempCacheRoot);
        _pathsMock.Setup(p => p.ProjectCacheDir(It.IsAny<string>())).Returns(_tempCacheRoot);
        _sourceFactoryMock.Setup(f => f.Create(It.IsAny<SourceConfig>())).Returns(_sourceProviderMock.Object);
        _sut = new SandboxLanguageResolver(
            _pathsMock.Object,
            _sourceFactoryMock.Object,
            NullLogger<SandboxLanguageResolver>.Instance);
    }

    [Fact]
    public async Task ResolveAsync_CacheHasCsharp_ReturnsCsharpFromHostCacheLayer()
    {
        WriteCacheMap(primaryLanguage: "csharp");

        var result = await _sut.ResolveAsync(_source, CancellationToken.None);

        result.Language.Should().Be("csharp");
        result.Layer.Should().Be(SandboxToolchainResolutionLayer.HostCache);
    }

    [Fact]
    public async Task ResolveAsync_CacheMissing_FallsThroughToRemoteContextYaml()
    {
        _sourceProviderMock.Setup(p => p.TryReadFileAsync(".agentsmith/context.yaml", It.IsAny<CancellationToken>()))
            .ReturnsAsync("stack:\n  lang: TypeScript\n");

        var result = await _sut.ResolveAsync(_source, CancellationToken.None);

        result.Language.Should().Be("TypeScript");
        result.Layer.Should().Be(SandboxToolchainResolutionLayer.RemoteContextYaml);
    }

    [Fact]
    public async Task ResolveAsync_RemoteContextYamlHasCsharp_ReturnsCsharpFromRemoteLayer()
    {
        _sourceProviderMock.Setup(p => p.TryReadFileAsync(".agentsmith/context.yaml", It.IsAny<CancellationToken>()))
            .ReturnsAsync("meta:\n  project: x\nstack:\n  lang: C#\n  runtime: .NET 8\n");

        var result = await _sut.ResolveAsync(_source, CancellationToken.None);

        result.Language.Should().Be("C#");
        result.Layer.Should().Be(SandboxToolchainResolutionLayer.RemoteContextYaml);
    }

    [Fact]
    public async Task ResolveAsync_AllLayersMiss_ReturnsNullFromGenericFallbackLayer()
    {
        _sourceProviderMock.Setup(p => p.TryReadFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var result = await _sut.ResolveAsync(_source, CancellationToken.None);

        result.Language.Should().BeNull();
        result.Layer.Should().Be(SandboxToolchainResolutionLayer.GenericFallback);
    }

    [Fact]
    public async Task ResolveAsync_ContextYamlMalformed_ReturnsNullWithoutThrow()
    {
        _sourceProviderMock.Setup(p => p.TryReadFileAsync(".agentsmith/context.yaml", It.IsAny<CancellationToken>()))
            .ReturnsAsync("not: valid: yaml: at: all: :::");

        var result = await _sut.ResolveAsync(_source, CancellationToken.None);

        result.Language.Should().BeNull();
        result.Layer.Should().Be(SandboxToolchainResolutionLayer.GenericFallback);
    }

    [Fact]
    public async Task ResolveAsync_ContextYamlPrimaryLanguageNotString_ReturnsNullWithoutThrow()
    {
        // stack.lang is a sequence here, not a scalar — YamlDotNet's strict
        // deserializer raises during conversion; the resolver swallows it.
        _sourceProviderMock.Setup(p => p.TryReadFileAsync(".agentsmith/context.yaml", It.IsAny<CancellationToken>()))
            .ReturnsAsync("stack:\n  lang:\n    - C#\n    - F#\n");

        var result = await _sut.ResolveAsync(_source, CancellationToken.None);

        result.Language.Should().BeNull();
        result.Layer.Should().Be(SandboxToolchainResolutionLayer.GenericFallback);
    }

    [Fact]
    public async Task ResolveAsync_RemoteThrows_ReturnsNullAndDoesNotCascade()
    {
        _sourceProviderMock.Setup(p => p.TryReadFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("connection refused"));

        var result = await _sut.ResolveAsync(_source, CancellationToken.None);

        result.Language.Should().BeNull();
        result.Layer.Should().Be(SandboxToolchainResolutionLayer.GenericFallback);
    }

    private void WriteCacheMap(string primaryLanguage)
    {
        // Mirrors AnalyzeProjectHandler.JsonOptions exactly so the test asserts
        // against the production write-format, not a parallel guess.
        var json = JsonSerializer.Serialize(
            new
            {
                primary_language = primaryLanguage,
                frameworks = Array.Empty<string>(),
                modules = Array.Empty<object>(),
                test_projects = Array.Empty<object>(),
                entry_points = Array.Empty<string>(),
                conventions = new { },
                ci = new { }
            },
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
        File.WriteAllText(Path.Combine(_tempCacheRoot, "project-map.json"), json);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempCacheRoot, recursive: true); } catch { /* best-effort */ }
    }
}
