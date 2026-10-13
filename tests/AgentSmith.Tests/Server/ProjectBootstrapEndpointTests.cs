using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Server.Extensions;
using AgentSmith.Server.Services.Init;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Server;

/// <summary>2026-10-13-0b82c: GET /api/projects/{name}/bootstrap, driven through the real reader and probe.</summary>
public sealed class ProjectBootstrapEndpointTests
{
    private readonly Mock<ISourceProvider> _host = new();
    private readonly List<BranchName?> _branchesRead = [];
    private RepoConnection _repo = new() { Name = "server", Url = "https://github.com/o/server", Type = RepoType.GitHub };

    public ProjectBootstrapEndpointTests()
    {
        _host.Setup(p => p.BranchExistsAsync(It.IsAny<BranchName>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        Files(principles: true);
    }

    private void Files(bool principles)
    {
        string? Answer(string path) => path.EndsWith("/principles.md") && !principles ? null
            : path.EndsWith("/coding-principles.md") ? "# old" : "content";
        _host.Setup(p => p.TryReadFileAsync(It.IsAny<string>(), It.IsAny<BranchName>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string path, BranchName b, CancellationToken _) => { _branchesRead.Add(b); return Answer(path); });
        _host.Setup(p => p.TryReadFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string path, CancellationToken _) => { _branchesRead.Add(null); return Answer(path); });
    }

    private async Task<IResult> GetAsync(string name = "sample")
    {
        var loader = new Mock<IConfigurationLoader>();
        loader.Setup(l => l.LoadConfig(It.IsAny<string>())).Returns(new AgentSmithConfig
        {
            Projects = new Dictionary<string, ResolvedProject> { ["sample"] = new() { Name = "sample", Repos = [_repo] } },
        });
        var hosts = new Mock<ISourceProviderFactory>();
        hosts.Setup(f => f.Create(It.IsAny<RepoConnection>())).Returns(_host.Object);
        var contexts = new Mock<ISandboxLanguageResolver>();
        contexts.Setup(c => c.ListContextsAsync(It.IsAny<RepoConnection>(), It.IsAny<ContextReadBranches>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RemoteContextListing([new RemoteContextDiscovery("api", ".", "csharp")]));
        var reader = new ProjectBootstrapReader(loader.Object, new ServerContext("c.yml"),
            new RemoteBootstrapProbe(hosts.Object, contexts.Object, NullLogger<RemoteBootstrapProbe>.Instance));
        return await ProjectBootstrapEndpoints.GetAsync(name, reader, CancellationToken.None);
    }

    private async Task<ProjectBootstrapEntry> OnlyEntryAsync() =>
        (await GetAsync()).Should().BeOfType<Ok<IReadOnlyList<ProjectBootstrapEntry>>>().Subject.Value.Should().ContainSingle().Subject;

    [Fact]
    public async Task ProjectBootstrap_CompleteRepo_IsComplete()
    {
        var entry = await OnlyEntryAsync();

        entry.State.Should().Be("complete");
        entry.Branch.Should().Be("default");
    }

    [Fact]
    public async Task ProjectBootstrap_IncompleteRepo_ListsMissingFiles()
    {
        Files(principles: false);

        var entry = await OnlyEntryAsync();

        entry.State.Should().Be("incomplete");
        entry.Missing.Should().ContainSingle().Which.Should().Contain("'api' has no principles.md").And.Contain("retired coding-principles.md");
    }

    [Fact]
    public async Task ProjectBootstrap_HostError_IsUnknownWithReason()
    {
        _host.Setup(p => p.TryReadFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException("401 Unauthorized"));

        var entry = await OnlyEntryAsync();

        entry.State.Should().Be("unknown");
        entry.Reason.Should().Contain("401");
    }

    [Fact]
    public async Task ProjectBootstrap_ConfiguredDefaultBranch_IsReadAndNamed()
    {
        _repo = _repo with { DefaultBranch = "develop" };

        var entry = await OnlyEntryAsync();

        entry.Branch.Should().Be("develop");
        _branchesRead.Should().NotBeEmpty().And.OnlyContain(b => b != null && b.Value == "develop");
    }

    [Fact]
    public async Task ProjectBootstrap_RepoWithoutLocation_IsUnknown()
    {
        _repo = new RepoConnection { Name = "server", Type = RepoType.Local };

        (await OnlyEntryAsync()).State.Should().Be("unknown");
    }

    [Fact]
    public async Task ProjectBootstrap_UnknownProject_404() =>
        (await GetAsync("nope")).Should().BeOfType<NotFound>();
}
