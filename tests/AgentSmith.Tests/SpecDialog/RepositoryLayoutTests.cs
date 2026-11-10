using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-11-09-0620b: a repository with no cached code map shows its top two levels where the map would stand, read
/// remotely; a repository that cannot be listed keeps the one line that sends the turn to its tools.
/// </summary>
public sealed class RepositoryLayoutTests
{
    internal static RepositoryLayoutReader Reader(ISourceProviderFactory providers) =>
        new(providers, TimeProvider.System, NullLogger<RepositoryLayoutReader>.Instance);

    [Fact]
    public async Task LoadCachedCodeMap_RepoWithoutMap_RendersItsTwoLevelLayout()
    {
        var provider = new Mock<ISourceProvider>();
        provider.Setup(p => p.ListDirectoryAsync("", It.IsAny<CancellationToken>())).ReturnsAsync(["Sample.Api", "README.md"]);
        provider.Setup(p => p.ListDirectoryAsync("Sample.Api", It.IsAny<CancellationToken>()))
            .ReturnsAsync(["Extensions", "Program.cs"]);
        provider.Setup(p => p.ListDirectoryAsync("README.md", It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var maps = await RunAsync(provider);

        maps["sample"].Should().Contain("No code map is cached for this repository yet")
            .And.Contain("- Sample.Api/\n  - Extensions\n  - Program.cs").And.Contain("- README.md");
    }

    [Fact]
    public async Task LoadCachedCodeMap_ListingFails_KeepsTheOneLiner()
    {
        var provider = new Mock<ISourceProvider>();
        provider.Setup(p => p.ListDirectoryAsync("", It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException("401"));

        var maps = await RunAsync(provider);

        maps["sample"].Should().Contain("no cached code map");
    }

    // 2026-11-09-0620l: two projects may name different repositories alike; a cached layout is never shared by name.
    [Fact]
    public async Task RepositoryLayout_SameNameOtherRemote_ListsItsOwnTree()
    {
        var providers = new Mock<ISourceProviderFactory>();
        providers.Setup(f => f.Create(It.Is<RepoConnection>(r => r.Url!.Contains("one")))).Returns(Listing("OneApi"));
        providers.Setup(f => f.Create(It.Is<RepoConnection>(r => r.Url!.Contains("two")))).Returns(Listing("TwoApi"));
        var reader = Reader(providers.Object);

        var one = await reader.ReadAsync(new RepoConnection { Name = "server", Url = "https://example.invalid/one.git" }, default);
        var two = await reader.ReadAsync(new RepoConnection { Name = "server", Url = "https://example.invalid/two.git" }, default);

        one.Should().Contain("OneApi");
        two.Should().Contain("TwoApi").And.NotContain("OneApi");
    }

    private static ISourceProvider Listing(string entry)
    {
        var provider = new Mock<ISourceProvider>();
        provider.Setup(p => p.ListDirectoryAsync("", It.IsAny<CancellationToken>())).ReturnsAsync([entry]);
        provider.Setup(p => p.ListDirectoryAsync(entry, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        return provider.Object;
    }

    private static async Task<IReadOnlyDictionary<string, string>> RunAsync(Mock<ISourceProvider> provider)
    {
        var providers = new Mock<ISourceProviderFactory>();
        providers.Setup(f => f.Create(It.IsAny<RepoConnection>())).Returns(provider.Object);
        var store = new Mock<IProjectMapStore>();
        store.Setup(s => s.ListByPrefixAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos,
            [new RepoConnection { Name = "sample", Url = "https://example.invalid/sample.git" }]);

        await new LoadCachedCodeMapHandler(store.Object, Reader(providers.Object), NullLogger<LoadCachedCodeMapHandler>.Instance)
            .ExecuteAsync(new LoadCachedCodeMapContext(pipeline), CancellationToken.None);

        return pipeline.Get<IReadOnlyDictionary<string, string>>(ContextKeys.RepoCodeMaps);
    }
}
