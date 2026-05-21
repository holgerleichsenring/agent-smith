using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Commands;

/// <summary>
/// p0158c spec-driven behaviour: PR cross-link pass-2 replaces the
/// sibling-PRs marker in each opened PR body with the actual sibling URL list
/// via the source provider's UpdatePullRequestBodyAsync. Single-PR runs skip
/// the PATCH pass entirely; patch failures are logged but do not fail the
/// pipeline.
/// </summary>
public sealed class PrCrossLinkHandlerTests
{
    [Fact]
    public async Task PrCrossLinks_TwoPass_AllOpenedPrBodies_PatchedWithSiblingUrls()
    {
        var harness = new Harness()
            .WithRepo("a", "https://x/a/pull/1", "body-a <!-- agentsmith:sibling-prs -->")
            .WithRepo("b", "https://x/b/pull/2", "body-b <!-- agentsmith:sibling-prs -->");

        var result = await harness.RunAsync();

        result.IsSuccess.Should().BeTrue();
        harness.PatchedBodies.Should().HaveCount(2);
        harness.PatchedBodies["https://x/a/pull/1"].Should().Contain("https://x/b/pull/2");
        harness.PatchedBodies["https://x/b/pull/2"].Should().Contain("https://x/a/pull/1");
        harness.PatchedBodies.Values.Should().AllSatisfy(b => b.Should().NotContain("<!-- agentsmith:sibling-prs -->"));
    }

    [Fact]
    public async Task PrCrossLinks_SinglePrRun_SkipsPass2()
    {
        var harness = new Harness().WithRepo("solo", "https://x/solo/pull/1", "body <!-- agentsmith:sibling-prs -->");

        var result = await harness.RunAsync();

        result.IsSuccess.Should().BeTrue();
        harness.PatchedBodies.Should().BeEmpty("single-PR run does not patch anything");
    }

    [Fact]
    public async Task PrCrossLinks_FailedOpen_ShownAsOpenFailed_InSurvivingSiblingBodies()
    {
        var harness = new Harness()
            .WithRepo("ok", "https://x/ok/pull/1", "body <!-- agentsmith:sibling-prs -->")
            .WithRepo("ok2", "https://x/ok2/pull/2", "body <!-- agentsmith:sibling-prs -->")
            .WithFailedOpen("broken");

        var result = await harness.RunAsync();

        result.IsSuccess.Should().BeTrue();
        harness.PatchedBodies.Values.Should().AllSatisfy(b => b.Should().Contain("broken: (open failed)"));
    }

    [Fact]
    public async Task PrCrossLinks_PatchFailure_LoggedButDoesNotFailPipeline()
    {
        var harness = new Harness()
            .WithRepo("a", "https://x/a/pull/1", "body <!-- agentsmith:sibling-prs -->")
            .WithRepo("b", "https://x/b/pull/2", "body <!-- agentsmith:sibling-prs -->")
            .WithPatchFailureFor("b");

        var result = await harness.RunAsync();

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("1/2");
    }

    [Fact]
    public async Task PrCrossLinks_NoOpenedPullRequests_OkAndNoPatch()
    {
        var harness = new Harness();

        var result = await harness.RunAsync();

        result.IsSuccess.Should().BeTrue();
        harness.PatchedBodies.Should().BeEmpty();
    }

    private sealed class Harness
    {
        public PipelineContext Pipeline { get; } = new();
        public Dictionary<string, string> PatchedBodies { get; } = new();

        private readonly List<RepoConnection> _repos = new();
        private readonly List<OpenedPullRequest> _opened = new();
        private readonly Dictionary<string, string> _bodies = new();
        private readonly HashSet<string> _patchFailures = new(StringComparer.Ordinal);
        private readonly Mock<ISourceProviderFactory> _sourceFactoryMock = new();

        public Harness WithRepo(string name, string prUrl, string body)
        {
            _repos.Add(new RepoConnection { Name = name, Type = RepoType.GitHub, Url = $"https://x/{name}.git" });
            _opened.Add(new OpenedPullRequest(name, prUrl, OpenStatus.Opened));
            _bodies[name] = body;
            SetupProvider(name);
            return this;
        }

        public Harness WithFailedOpen(string name)
        {
            _repos.Add(new RepoConnection { Name = name, Type = RepoType.GitHub, Url = $"https://x/{name}.git" });
            _opened.Add(new OpenedPullRequest(name, Url: null, OpenStatus.Failed));
            SetupProvider(name);
            return this;
        }

        public Harness WithPatchFailureFor(string name)
        {
            _patchFailures.Add(name);
            return this;
        }

        private void SetupProvider(string name)
        {
            var providerMock = new Mock<ISourceProvider>();
            providerMock.Setup(p => p.UpdatePullRequestBodyAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns<string, string, CancellationToken>((url, body, _) =>
                {
                    if (_patchFailures.Contains(name)) return Task.FromResult(false);
                    PatchedBodies[url] = body;
                    return Task.FromResult(true);
                });
            _sourceFactoryMock.Setup(f => f.Create(It.Is<RepoConnection>(r => r.Name == name)))
                .Returns(providerMock.Object);
        }

        public Task<CommandResult> RunAsync()
        {
            Pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, _repos);
            Pipeline.Set<IReadOnlyList<OpenedPullRequest>>(ContextKeys.OpenedPullRequests, _opened);
            Pipeline.Set<IReadOnlyDictionary<string, string>>(ContextKeys.OpenedPullRequestBodies, _bodies);
            var handler = new PrCrossLinkHandler(
                _sourceFactoryMock.Object, NullLogger<PrCrossLinkHandler>.Instance);
            return handler.ExecuteAsync(new PrCrossLinkContext(_repos, Pipeline), CancellationToken.None);
        }
    }
}
