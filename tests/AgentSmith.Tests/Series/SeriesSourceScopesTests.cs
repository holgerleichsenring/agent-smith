using AgentSmith.Application.Services.Series;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Series;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services.Series;
using AgentSmith.Tests.Persistence.ReferenceFiles;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Series;

/// <summary>2026-11-27-3532c: the series master's read scopes — every project repository at the default branch, at the
/// branch of each ticket a run or pull request worked and at the series branch, and the conversation's uploads; none of
/// the uploads without a conversation; a map that lists each address once and disposes every scope it was given.</summary>
public sealed class SeriesSourceScopesTests
{
    private static readonly RepoConnection Api = new() { Name = "api" };
    private static readonly RepoConnection Ui = new() { Name = "group/ui" };
    private static readonly ResolvedProject Project = new() { Name = "sample", Repos = [Api, Ui] };
    private readonly Mock<ISourceScopeSandboxFactory> _scopes = new();
    private readonly Mock<IReferenceSetSandboxFactory> _references = new();

    public SeriesSourceScopesTests()
    {
        _scopes.Setup(f => f.Create(It.IsAny<ResolvedProject>(), It.IsAny<RepoConnection>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(() => Mock.Of<ISourceScopeSandbox>());
        _references.Setup(f => f.Create(It.IsAny<ResolvedProject>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<ReferenceCoverage?>())).Returns(() => Mock.Of<ISourceScopeSandbox>());
    }

    [Fact]
    public async Task Open_TicketsSeriesAndUploads_KeysEveryAddress()
    {
        await using var store = await ReferenceFileStore.OpenAsync(ReferenceFileStore.Sqlite);
        await using var db = store.Context();
        var sets = new ReferenceSetRepository(db);
        var site = await sets.AddAsync("conv-1", [Css()], default);
        var record = SeriesRecords.Running() with { IntegrationBranch = "agent-smith/series/sr-1" };

        await using var map = await Scopes(sets).OpenAsync(Beat(record, SeriesRecords.Fact("43", "running", finished: false),
            SeriesRecords.Fact("44")), Project, default);

        map.Sources.Select(s => s.Address).Should().Equal(
            "api", "api@SCRUM-43", "api@series", "group/ui", "group/ui@SCRUM-43", "group/ui@series", "reference:my-site");
        _scopes.Verify(f => f.Create(Project, Api, null, null));
        _scopes.Verify(f => f.Create(Project, Api, "agent-smith/43", null), "a ticket's branch is named by its id, never its key");
        _scopes.Verify(f => f.Create(Project, Ui, "agent-smith/series/sr-1", null));
        _scopes.Verify(f => f.Create(Project, Api, "agent-smith/44", It.IsAny<string?>()), Times.Never(), "no run or pull request worked SCRUM-44");
        _scopes.Verify(f => f.Create(It.IsAny<ResolvedProject>(), It.IsAny<RepoConnection>(), It.IsAny<string?>(), It.IsNotNull<string>()),
            Times.Never(), "a repository scope is the beat's own, never a design conversation's held sandbox");
        _references.Verify(f => f.Create(Project, "conv-1", "reference:my-site", site.SetId, null));
        map.ReadTools().Select(t => t.Name).Should().BeEquivalentTo(["read_file", "grep_in_tree", "find_files", "directory_tree"]);
    }

    [Fact]
    public async Task Open_NoConversation_NoReferenceKeys()
    {
        await using var store = await ReferenceFileStore.OpenAsync(ReferenceFileStore.Sqlite);
        await using var db = store.Context();
        var sets = new ReferenceSetRepository(db);
        await sets.AddAsync(string.Empty, [Css()], default);

        await using var map = await Scopes(sets).OpenAsync(Beat(SeriesRecords.Running(conversation: string.Empty)), Project, default);

        map.Sources.Should().NotBeEmpty().And.NotContain(s => s.Address.StartsWith("reference:"));
        map.Sources.Select(s => s.Address).Should().NotContain(a => a.EndsWith("@series"), "a series with no series branch has none");
        _references.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Open_NoRepositories_NothingToRead()
    {
        await using var map = await Scopes(sets: null).OpenAsync(Beat(SeriesRecords.Running()), new ResolvedProject { Name = "empty" }, default);

        map.Sources.Should().BeEmpty();
        map.ReadTools().Should().BeEmpty();
    }

    [Fact]
    public async Task Map_ADuplicateAddress_ListedOnceAndEveryScopeDisposed()
    {
        var first = new Mock<ISourceScopeSandbox>();
        var second = new Mock<ISourceScopeSandbox>();
        first.Setup(s => s.DisposeAsync()).ThrowsAsync(new InvalidOperationException("gone"));

        var map = new SeriesSourceMap([new("api", "a", first.Object), new("api", "b", second.Object)], NullLogger.Instance);
        await map.DisposeAsync();

        map.Sources.Should().ContainSingle().Which.Shows.Should().Be("a");
        second.Verify(s => s.DisposeAsync(), Times.Once(), "one scope failing to dispose does not leak the rest");
    }

    private static SeriesBeat Beat(SeriesRunRecord record, params SeriesTicketFact[] facts) => new(record, facts, SeriesRecords.T);

    private static ReferenceFile Css() => new() { RelativePath = "My Site/a.css", MediaType = "text/css", Content = [0x61] };

    private SeriesSourceScopes Scopes(ReferenceSetRepository? sets)
    {
        var services = new ServiceCollection();
        if (sets is not null) services.AddSingleton(sets);
        return new(_scopes.Object, _references.Object, services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            NullLogger<SeriesSourceScopes>.Instance);
    }
}
