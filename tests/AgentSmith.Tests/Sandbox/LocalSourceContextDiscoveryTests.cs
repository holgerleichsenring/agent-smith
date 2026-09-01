using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Infrastructure.Services;
using AgentSmith.Infrastructure.Services.Providers.Source;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// 2026-09-01-1335: discovery for a source located by PATH instead of by url — the shape
/// `agentsmith demo` and `--source-type local --source-path X` produce. It used to
/// short-circuit on the empty url and hand back the synthetic default, so a local run lost
/// the image and the verify stages its own context.yaml declares. Real LocalSourceProvider
/// and real ContextYamlParser, so the file IO and the YAML parse are the production ones.
/// </summary>
public sealed class LocalSourceContextDiscoveryTests : IDisposable
{
    private const string DeclaredYaml = """
        meta:
          workdir: .
          purpose: "Two tiny csproj files a first run can build and test end to end."
        stack:
          lang: csharp
          image: mcr.microsoft.com/dotnet/sdk:10.0
        verify:
          - label: build
            command: dotnet build src/Sample/Sample.csproj
          - label: test
            command: dotnet test tests/Sample.Tests/Sample.Tests.csproj
        """;

    private readonly string _workingCopy = Path.Combine(
        Path.GetTempPath(), "agentsmith-local-discovery-" + Guid.NewGuid().ToString("N")[..8]);

    public LocalSourceContextDiscoveryTests() => Directory.CreateDirectory(_workingCopy);

    [Fact]
    public async Task Discovery_ALocalSourceDeclaringAContext_CarriesItsImageAndStages()
    {
        await WriteContextAsync("default", DeclaredYaml);

        var result = await NewResolver().ResolveAllAsync(LocalSource(), CancellationToken.None);

        var discovery = result.Should().ContainSingle().Subject;
        discovery.ContextName.Should().Be("default");
        discovery.Language.Should().Be("csharp");
        discovery.ToolchainImage.Should().Be("mcr.microsoft.com/dotnet/sdk:10.0");
        discovery.Verify.Should().NotBeNull();
        discovery.Verify!.Select(s => s.Label).Should().Equal("build", "test");
        discovery.Verify.Select(s => s.Command)
            .Should().Contain("dotnet build src/Sample/Sample.csproj");
    }

    [Fact]
    public async Task Discovery_ALocalSourceWithNoContexts_StillFallsBackToTheSyntheticDefault()
    {
        var result = await NewResolver().ResolveAllAsync(LocalSource(), CancellationToken.None);

        result.Should().ContainSingle()
            .Which.Should().Be(new RemoteContextDiscovery("default", ".", null));
    }

    [Fact]
    public async Task Discovery_ALocalSourceDeclaringAContext_IsReportedAsReadable()
    {
        await WriteContextAsync("default", DeclaredYaml);

        var listing = await NewResolver().ListContextsAsync(LocalSource(), CancellationToken.None);

        listing.IsUnreadable.Should().BeFalse();
        listing.Contexts.Should().ContainSingle().Which.Verify.Should().HaveCount(2);
    }

    private RepoConnection LocalSource() => new()
    {
        Name = "demo",
        Type = RepoType.Local,
        Path = _workingCopy,
    };

    private async Task WriteContextAsync(string contextName, string yaml)
    {
        var dir = Path.Combine(_workingCopy, ".agentsmith", "contexts", contextName);
        Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(Path.Combine(dir, "context.yaml"), yaml);
    }

    // Mirrors SourceProviderFactory.CreateLocal: a Local connection is served by a
    // LocalSourceProvider rooted at its own Path — the routing this phase relies on.
    private static SandboxLanguageResolver NewResolver()
    {
        var factory = new Mock<ISourceProviderFactory>();
        factory.Setup(f => f.Create(It.IsAny<RepoConnection>()))
            .Returns((RepoConnection c) => new LocalSourceProvider(c.Path!, c.DefaultBranch));
        return new SandboxLanguageResolver(
            factory.Object,
            new ContextYamlParser(new ContextYamlSerializer(new ContextYamlBuilders())),
            NullLogger<SandboxLanguageResolver>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_workingCopy)) Directory.Delete(_workingCopy, recursive: true);
    }
}
