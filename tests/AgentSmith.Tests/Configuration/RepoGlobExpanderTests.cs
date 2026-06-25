using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AgentSmith.Tests.Configuration;

public class RepoGlobExpanderTests
{
    [Theory]
    [InlineData("Sample.App.*", "Sample.App.Server", true)]
    [InlineData("Sample.App.*", "Sample.Translate.Server", false)]
    [InlineData("exact-name", "exact-name", true)]
    [InlineData("exact-name", "Exact-Name", true)]    // case-insensitive
    [InlineData("*.Docs", "Product.Docs", true)]
    public void Matcher_Glob_MatchesExpected(string pattern, string name, bool expected) =>
        RepoGlobMatcher.IsMatch(pattern, name).Should().Be(expected);

    [Theory]
    [InlineData("conn/Foo.*", false, "conn", "Foo.*")]
    [InlineData("!conn/Foo.Tests", true, "conn", "Foo.Tests")]
    public void Ref_Parse_SplitsConnectionPatternAndExclude(string entry, bool isExclude, string conn, string pattern)
    {
        RepoGlobRef.IsConnectionRef(entry).Should().BeTrue();
        var parsed = RepoGlobRef.Parse(entry);
        parsed.IsExclude.Should().Be(isExclude);
        parsed.Connection.Should().Be(conn);
        parsed.Pattern.Should().Be(pattern);
    }

    [Fact]
    public void Ref_BareName_IsNotConnectionRef() =>
        RepoGlobRef.IsConnectionRef("legacy-repo-name").Should().BeFalse();

    [Fact]
    public void Expand_GlobWithExclude_SelectsMatchingMinusExcluded()
    {
        var snapshot = Seeded("conn",
            Repo("Sample.App.Server"), Repo("Sample.App.Client"),
            Repo("Sample.App.Ui.Tests"), Repo("Sample.Translate.Server"));
        var expander = new RepoGlobExpander(snapshot, new ThrowingRefresher(), NullLogger<RepoGlobExpander>.Instance);
        var refs = new[]
        {
            RepoGlobRef.Parse("conn/Sample.App.*"),
            RepoGlobRef.Parse("!conn/Sample.App.Ui.Tests"),
        };

        var result = expander.Expand("p", refs, Connections("conn"));

        result.Select(r => r.Name).Should().BeEquivalentTo("Sample.App.Server", "Sample.App.Client");
    }

    [Fact]
    public void Expand_ColdSnapshot_TriggersRefresher_ThenResolves()
    {
        var snapshot = new InMemoryConnectionRepoSnapshot();
        var refresher = new SeedingRefresher(snapshot, "conn", Repo("Sample.App.Server"));
        var expander = new RepoGlobExpander(snapshot, refresher, NullLogger<RepoGlobExpander>.Instance);

        var result = expander.Expand("p", new[] { RepoGlobRef.Parse("conn/Sample.App.*") }, Connections("conn"));

        refresher.Called.Should().BeTrue();
        result.Should().ContainSingle().Which.Name.Should().Be("Sample.App.Server");
    }

    [Fact]
    public void Expand_UnknownConnection_Throws()
    {
        var expander = new RepoGlobExpander(
            new InMemoryConnectionRepoSnapshot(), new ThrowingRefresher(), NullLogger<RepoGlobExpander>.Instance);

        var act = () => expander.Expand("p", new[] { RepoGlobRef.Parse("ghost/Foo.*") }, Connections("conn"));

        act.Should().Throw<AgentSmith.Domain.Exceptions.ConfigurationException>().WithMessage("*connection 'ghost'*");
    }

    [Fact]
    public void Expand_BuildsRepoConnectionFromConnectionAndDiscoveredRepo()
    {
        var snapshot = Seeded("conn", new DiscoveredRepo
        {
            Name = "Sample.App.Docs", Url = "https://dev.azure.com/x/y/_git/Sample.App.Docs", DefaultBranch = "main",
        });
        var expander = new RepoGlobExpander(snapshot, new ThrowingRefresher(), NullLogger<RepoGlobExpander>.Instance);

        var repo = expander.Expand("p", new[] { RepoGlobRef.Parse("conn/*") }, Connections("conn")).Single();

        repo.Type.Should().Be(RepoType.AzureDevOps);
        repo.Url.Should().Be("https://dev.azure.com/x/y/_git/Sample.App.Docs");
        repo.Organization.Should().Be("Org");
        repo.DefaultBranch.Should().Be("main");      // discovered branch wins over connection default
    }

    private static DiscoveredRepo Repo(string name) => new() { Name = name, Url = $"https://x/{name}" };

    private static InMemoryConnectionRepoSnapshot Seeded(string connection, params DiscoveredRepo[] repos)
    {
        var s = new InMemoryConnectionRepoSnapshot();
        s.Set(connection, repos);
        return s;
    }

    private static Dictionary<string, ResolvedConnection> Connections(string name) => new()
    {
        [name] = new ResolvedConnection
        {
            Name = name, Type = RepoType.AzureDevOps, Organization = "Org", Project = "Proj", DefaultBranch = "develop",
        },
    };

    private sealed class ThrowingRefresher : IRepoDiscoveryRefresher
    {
        public Task RefreshAsync(ResolvedConnection c, CancellationToken ct) =>
            throw new InvalidOperationException("should not refresh — snapshot is warm");
        public Task RefreshAllAsync(IReadOnlyCollection<ResolvedConnection> c, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class SeedingRefresher(IConnectionRepoSnapshot snapshot, string connection, params DiscoveredRepo[] repos)
        : IRepoDiscoveryRefresher
    {
        public bool Called { get; private set; }
        public Task RefreshAsync(ResolvedConnection c, CancellationToken ct)
        {
            Called = true;
            snapshot.Set(connection, repos);
            return Task.CompletedTask;
        }
        public Task RefreshAllAsync(IReadOnlyCollection<ResolvedConnection> c, CancellationToken ct) => Task.CompletedTask;
    }
}
