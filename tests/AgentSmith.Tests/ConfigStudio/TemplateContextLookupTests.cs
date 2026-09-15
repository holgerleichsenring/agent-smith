using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Services.Config;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.ConfigStudio;

/// <summary>
/// 2026-09-14-620e: the studio's template form offers context names instead of asking an
/// operator to remember them. The lookup keeps the two answers a listing can give apart —
/// "declares nothing" and "could not be read" — and never serves a provider's own error
/// text unedited.
/// </summary>
public sealed class TemplateContextLookupTests
{
    private const string Project = "app";
    private const string RepoRef = "reference";

    private readonly Mock<IConfigurationLoader> _loader = new();
    private readonly Mock<ISandboxLanguageResolver> _contexts = new();
    private readonly TemplateContextLookup _sut;

    public TemplateContextLookupTests()
    {
        _loader.Setup(l => l.LoadConfig(It.IsAny<string>())).Returns(ConfigWith(
            new RepoConnection { Name = RepoRef, Url = "https://git.test/reference" }));
        _sut = new TemplateContextLookup(_loader.Object, _contexts.Object);
    }

    [Fact]
    public async Task ListContexts_RepoWithTwoContexts_ReturnsBothNames()
    {
        Listing(new RemoteContextListing([
            new RemoteContextDiscovery("server", "src/Server", "csharp"),
            new RemoteContextDiscovery("client", "src/Client", null),
        ]));

        var view = await _sut.ListAsync(Project, RepoRef, CancellationToken.None);

        view!.Contexts.Should().Equal("server", "client");
        view.UnreadableReason.Should().BeNull();
    }

    [Fact]
    public async Task ListContexts_RepoWithoutTheDirectory_ReturnsEmptyAndSaysSo()
    {
        Listing(RemoteContextListing.None);

        var view = await _sut.ListAsync(Project, RepoRef, CancellationToken.None);

        view!.Contexts.Should().BeEmpty();
        view.UnreadableReason.Should().BeNull(
            "a repository that declares nothing is readable — the form may offer a typed name, "
            + "but it must not tell the operator their credential failed");
    }

    [Fact]
    public async Task ListContexts_UnreachableRepo_ReportsTheReasonNotAnEmptyList()
    {
        Listing(RemoteContextListing.Unreadable("403 Forbidden"));

        var view = await _sut.ListAsync(Project, RepoRef, CancellationToken.None);

        view!.Contexts.Should().BeEmpty();
        view.UnreadableReason.Should().Be("403 Forbidden");
    }

    [Fact]
    public async Task ListContexts_UnreachableReason_CarriesNoCredential()
    {
        Listing(RemoteContextListing.Unreadable(
            "redirected to https://oauth2:ghp_notarealtoken@git.test/reference.git"));

        var view = await _sut.ListAsync(Project, RepoRef, CancellationToken.None);

        view!.UnreadableReason.Should().Be("redirected to https://git.test/reference.git");
    }

    [Fact]
    public async Task ListContexts_UnreachableReason_IsBounded()
    {
        Listing(RemoteContextListing.Unreadable(new string('x', 500)));

        var view = await _sut.ListAsync(Project, RepoRef, CancellationToken.None);

        view!.UnreadableReason!.Length.Should().Be(301, "300 characters and the ellipsis that says so");
    }

    [Fact]
    public async Task ListContexts_ConnectionScopedRef_MatchesTheResolvedRepoName()
    {
        Listing(new RemoteContextListing([new RemoteContextDiscovery("server", ".", null)]));

        var view = await _sut.ListAsync(Project, "conn/reference", CancellationToken.None);

        // ConnectionRepoUrlBuilder names the resolved repo after the REPO, never after the
        // ref that named it, so the ref's last segment is what a lookup has to match.
        view!.Contexts.Should().Equal(["server"]);
    }

    [Fact]
    public async Task ListContexts_UnknownProjectOrRepoRef_IsNotFound()
    {
        (await _sut.ListAsync("nosuchproject", RepoRef, CancellationToken.None)).Should().BeNull();
        (await _sut.ListAsync(Project, "nosuchrepo", CancellationToken.None)).Should().BeNull();
    }

    private void Listing(RemoteContextListing listing) =>
        _contexts.Setup(c => c.ListContextsAsync(It.IsAny<RepoConnection>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(listing);

    private static AgentSmithConfig ConfigWith(RepoConnection repo) => new()
    {
        Projects = new Dictionary<string, ResolvedProject>
        {
            [Project] = new() { Name = Project, Repos = [repo] },
        },
    };
}
