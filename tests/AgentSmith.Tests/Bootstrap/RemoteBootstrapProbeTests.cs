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
/// 2026-10-13-0b82b: the bootstrap gate's question asked of the host — the run's own context
/// discovery, then the gate's files on the probed branch, unknown whenever the host cannot say.
/// </summary>
public sealed class RemoteBootstrapProbeTests
{
    private const string Branch = "feature/x";
    private const string Yaml = "meta:\n  project: api\n  workdir: .\nstack:\n  lang: csharp\n";
    private readonly Mock<ISourceProvider> _provider = new();
    private readonly Dictionary<(string? Branch, string Path), string> _files = [];
    private readonly Dictionary<(string? Branch, string Path), List<string>> _dirs = [];
    private readonly List<(string? Branch, string Path)> _read = [];
    private static readonly RepoConnection Repo = new() { Name = "r", Url = "https://github.com/o/r", Type = RepoType.GitHub };

    public RemoteBootstrapProbeTests()
    {
        _provider.Setup(p => p.BranchExistsAsync(It.IsAny<BranchName>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _provider.Setup(p => p.TryReadFileAsync(It.IsAny<string>(), It.IsAny<BranchName>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string path, BranchName b, CancellationToken _) => Read(b.Value, path));
        _provider.Setup(p => p.TryReadFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string path, CancellationToken _) => Read(null, path));
        _provider.Setup(p => p.ListDirectoryAsync(It.IsAny<string>(), It.IsAny<BranchName>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string path, BranchName b, CancellationToken _) => List(b.Value, path));
        _provider.Setup(p => p.ListDirectoryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string path, CancellationToken _) => List(null, path));
    }

    private string? Read(string? branch, string path)
    {
        _read.Add((branch, path));
        return _files.GetValueOrDefault((branch, path));
    }

    private IReadOnlyList<string> List(string? branch, string path) => _dirs.GetValueOrDefault((branch, path)) ?? [];

    private void Context(string? branch, string name, bool contextYaml = true, bool principles = true, bool retired = false)
    {
        var dir = $".agentsmith/contexts/{name}";
        if (!_dirs.TryGetValue((branch, ".agentsmith/contexts"), out var children))
            _dirs[(branch, ".agentsmith/contexts")] = children = [];
        children.Add(name);
        if (contextYaml) _files[(branch, $"{dir}/context.yaml")] = Yaml;
        if (principles) _files[(branch, $"{dir}/principles.md")] = "# principles";
        if (retired) _files[(branch, $"{dir}/coding-principles.md")] = "# old";
    }

    private Task<RemoteBootstrapVerdict> ProbeAsync(string? branch = Branch)
    {
        var factory = new Mock<ISourceProviderFactory>();
        factory.Setup(f => f.Create(It.IsAny<RepoConnection>())).Returns(_provider.Object);
        var resolver = TestSandboxLanguageResolver.Create(factory.Object,
            new ContextYamlParser(new ContextYamlSerializer(new ContextYamlBuilders())));
        return new RemoteBootstrapProbe(factory.Object, resolver, NullLogger<RemoteBootstrapProbe>.Instance)
            .ProbeAsync(Repo, branch is null ? null : new BranchName(branch), CancellationToken.None);
    }

    [Fact]
    public async Task RemoteBootstrapProbe_Complete()
    {
        Context(Branch, "api");

        (await ProbeAsync()).State.Should().Be(RemoteBootstrapState.Complete);
    }

    [Fact]
    public async Task RemoteBootstrapProbe_RetiredPrinciples_ReportsRename()
    {
        Context(Branch, "api", principles: false, retired: true);

        var verdict = await ProbeAsync();

        verdict.State.Should().Be(RemoteBootstrapState.Incomplete);
        verdict.Missing.Should().ContainSingle().Which.Should().Be(new MissingBootstrapFile("api", "principles.md", RetiredPresent: true));
    }

    [Fact]
    public async Task RemoteBootstrapProbe_NoContexts_Incomplete()
    {
        var verdict = await ProbeAsync();

        verdict.State.Should().Be(RemoteBootstrapState.Incomplete);
        verdict.Missing.Select(m => m.ContextName).Should().OnlyContain(n => n == "default", "the run's synthetic context is what its gate reads");
    }

    [Fact]
    public async Task RemoteBootstrapProbe_StrayFileUnderContexts_IsNoContext()
    {
        Context(Branch, "api");
        _dirs[(Branch, ".agentsmith/contexts")].Add("README.md");

        (await ProbeAsync()).State.Should().Be(RemoteBootstrapState.Complete);
    }

    [Fact]
    public async Task RemoteBootstrapProbe_AbsentBranch_Unknown()
    {
        _provider.Setup(p => p.BranchExistsAsync(It.IsAny<BranchName>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var verdict = await ProbeAsync();

        verdict.State.Should().Be(RemoteBootstrapState.Unknown);
        verdict.Reason.Should().Contain(Branch);
    }

    [Fact]
    public async Task RemoteBootstrapProbe_ReadThrows_Unknown()
    {
        Context(Branch, "api");
        _provider.Setup(p => p.TryReadFileAsync(It.Is<string>(s => s.EndsWith("principles.md")), It.IsAny<BranchName>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("503"));

        (await ProbeAsync()).State.Should().Be(RemoteBootstrapState.Unknown);
    }

    [Fact]
    public async Task RemoteBootstrapProbe_BranchWithoutContexts_IncompleteOnThatBranch()
    {
        Context(null, "api");

        var verdict = await ProbeAsync();

        verdict.State.Should().Be(RemoteBootstrapState.Incomplete, "the run checks out the branch, which carries none of the default branch's files");
        verdict.Missing.Should().Contain(new MissingBootstrapFile("api", "context.yaml", false));
    }

    [Fact]
    public async Task RemoteBootstrapProbe_ReadsRepoRelativePaths()
    {
        Context(Branch, "api");

        await ProbeAsync();

        _read.Should().Contain((Branch, ".agentsmith/contexts/api/principles.md"));
        _read.Should().NotContain(r => r.Path.StartsWith('/'));
    }

    [Fact]
    public async Task RemoteBootstrapProbe_NoBranch_ReadsTheDefaultBranch()
    {
        Context(null, "api", principles: false);

        var verdict = await ProbeAsync(branch: null);

        verdict.State.Should().Be(RemoteBootstrapState.Incomplete);
        _read.Should().OnlyContain(r => r.Branch == null);
    }
}
