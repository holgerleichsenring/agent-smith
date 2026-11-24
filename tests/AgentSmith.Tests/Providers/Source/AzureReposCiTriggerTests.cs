using AgentSmith.Contracts.Reviews;
using AgentSmith.Infrastructure.Services.Providers.Source;
using FluentAssertions;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.Policy.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Moq;
using Newtonsoft.Json.Linq;

namespace AgentSmith.Tests.Providers.Source;

/// <summary>2026-11-24-f9c5d: Azure DevOps — the definitions whose build-validation policy covers the default branch of this
/// repository are queued on the pull request's source branch at its head; their builds there are its checks.</summary>
public sealed class AzureReposCiTriggerTests
{
    private const string PrUrl = "https://dev.azure.com/example/demo/_git/sample-server/pullrequest/7";
    private static readonly Guid ProjectId = Guid.NewGuid();
    private static readonly Guid RepoId = Guid.NewGuid();
    private readonly Mock<GitHttpClient> _git = Client<GitHttpClient>();
    private readonly Mock<BuildHttpClient> _build = Client<BuildHttpClient>();
    private readonly List<Build> _queued = [];
    private IReadOnlyList<PolicyConfiguration> _configs = [];

    public AzureReposCiTriggerTests()
    {
        _git.Setup(c => c.GetPullRequestAsync(It.IsAny<string>(), It.IsAny<string>(), 7, It.IsAny<int?>(), It.IsAny<int?>(),
                It.IsAny<int?>(), It.IsAny<bool?>(), It.IsAny<bool?>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GitPullRequest
            {
                PullRequestId = 7, SourceRefName = "refs/heads/agent-smith/42", LastMergeSourceCommit = new GitCommitRef { CommitId = "abc" },
                Repository = new GitRepository { Id = RepoId, ProjectReference = new TeamProjectReference { Id = ProjectId } },
            });
        _build.Setup(c => c.QueueBuildAsync(It.IsAny<Build>(), It.IsAny<bool?>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>(),
                It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<Build, bool?, string, int?, int?, object, CancellationToken>((b, _, _, _, _, _, _) => _queued.Add(b))
            .ReturnsAsync(new Build { Id = 99 });
    }

    [Fact]
    public async Task APolicyOnTheDefaultBranch_IsQueuedOnTheSourceBranchAtTheHead()
    {
        _configs = [Config(31, RepoId, "refs/heads/main"), Config(32, Guid.NewGuid(), "refs/heads/main"), Config(33, null, "refs/heads/release/", "prefix")];

        var answer = await StartAsync();

        answer.Outcome.Should().Be(CiTriggerOutcome.Started);
        answer.Url.Should().EndWith("_build/results?buildId=99");
        _queued.Should().ContainSingle().Which.Should().Match<Build>(b =>
            b.Definition.Id == 31 && b.SourceBranch == "refs/heads/agent-smith/42" && b.SourceVersion == "abc");
    }

    [Fact]
    public async Task NoPolicyCoversTheDefaultBranch_IsNoCi_AndQueuesNothing()
    {
        _configs = [Config(31, RepoId, "refs/heads/develop"), Config(34, RepoId, "refs/heads/main", enabled: false)];

        (await StartAsync()).Outcome.Should().Be(CiTriggerOutcome.NoCi);
        _queued.Should().BeEmpty();
    }

    [Fact]
    public void Validating_AScopeWithoutRepository_CoversEveryRepository_AndAPrefixMatches()
    {
        var found = AzureReposBuildPolicyScope.Validating(
            [Config(40, null, "refs/heads/main"), Config(41, RepoId, "refs/heads/ma", "prefix"), Config(42, RepoId, "refs/heads/main", deleted: true)],
            RepoId, "refs/heads/main");

        found.Select(v => v.DefinitionId).Should().Equal(40, 41);
    }

    [Fact]
    public void SeriesBuilds_NothingOnTheHead_IsNoCheck_OneRanIsItsResult()
    {
        IReadOnlyList<AzureReposBuildPolicyScope.Validation> validating = [new(31, "PR build", true), new(32, "lint", false)];
        Build Ran(int definition, string sha, BuildResult result) => new()
        {
            Id = definition * 10, Definition = new DefinitionReference { Id = definition }, SourceVersion = sha,
            Status = BuildStatus.Completed, Result = result,
        };

        AzureReposSeriesBuilds.Items(validating, [Ran(31, "old", BuildResult.Succeeded)], ProjectId, "abc", "https://dev.azure.com/example")
            .Should().BeEmpty("no CI on the head is what the trigger is started on");
        var items = AzureReposSeriesBuilds.Items(validating, [Ran(31, "abc", BuildResult.Failed)], ProjectId, "abc", "https://dev.azure.com/example");
        items.Select(i => (i.Check.Name, i.Check.Outcome, i.Check.Required)).Should().Equal(
            ("PR build", CiCheckOutcome.Failed, true), ("lint", CiCheckOutcome.NotStarted, false));
        items[0].BuildId.Should().Be(310);
    }

    private Task<CiTrigger> StartAsync() =>
        new AzureReposCiTrigger(_ => Task.FromResult(_git.Object), (_, _) => Task.FromResult(_configs), _ => Task.FromResult(_build.Object),
            "demo", "sample-server", "https://dev.azure.com/example").StartAsync(PrUrl, "main", default);

    private static PolicyConfiguration Config(int definition, Guid? repo, string refName, string matchKind = "exact",
        bool enabled = true, bool deleted = false) => new()
    {
        IsEnabled = enabled, IsDeleted = deleted, IsBlocking = true,
        Type = new PolicyTypeRef { Id = Guid.Parse(AzureReposCiPolicies.BuildPolicyType) },
        Settings = JObject.FromObject(new
        {
            buildDefinitionId = definition, displayName = $"build {definition}",
            scope = new[] { new { repositoryId = repo?.ToString(), refName, matchKind } },
        }),
    };

    private static Mock<T> Client<T>() where T : class =>
        new(new Uri("https://localhost/fake"), new VssCredentials(new VssBasicCredential(string.Empty, "fake")));
}
