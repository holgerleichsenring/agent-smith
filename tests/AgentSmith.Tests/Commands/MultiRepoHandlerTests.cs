using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Commands;

/// <summary>
/// p0158b spec-driven multi-repo behaviour: workdir layout, per-repo iteration,
/// per-repo PR open with empty-changes skip, OpenedPullRequests publication,
/// monorepo N=1 path equivalence.
/// </summary>
public sealed class MultiRepoHandlerTests
{
    [Fact]
    public async Task Checkout_PlacesEachRepo_InRunRootSubdirectory_KeyedByRepoName()
    {
        var harness = new CheckoutHarness();
        harness.AddRepo("server", "https://x/server.git");
        harness.AddRepo("client", "https://x/client.git");

        var result = await harness.RunAsync();

        result.IsSuccess.Should().BeTrue();
        harness.CloneWorkdirs.Should().BeEquivalentTo(new[] { "/work/server", "/work/client" });
    }

    [Fact]
    public async Task Checkout_LocalProvider_KeepsBindMountLayout_PerRepoSubdir()
    {
        var harness = new CheckoutHarness();
        harness.AddLocalRepo("only-repo", "/tmp");

        var result = await harness.RunAsync();

        result.IsSuccess.Should().BeTrue();
        var stored = harness.Pipeline.Get<Repository>(ContextKeys.Repository);
        stored.LocalPath.Should().Be("/work/only-repo");
    }

    [Fact]
    public async Task BranchCreate_SameName_AppliedToEveryCheckedOutRepo()
    {
        var harness = new CheckoutHarness(branch: "agent-smith/ticket-42");
        harness.AddRepo("a", "https://x/a.git");
        harness.AddRepo("b", "https://x/b.git");

        await harness.RunAsync();

        harness.CheckoutBranchSteps.Should().HaveCount(2);
        harness.CheckoutBranchSteps.Should().AllSatisfy(s =>
            s.Args!.Should().Contain("agent-smith/ticket-42"));
    }

    [Fact]
    public async Task CommitAndPR_SkipsRepo_WithNoStagedChanges()
    {
        var harness = new CommitAndPRHarness();
        harness.AddRepo("changed");
        harness.AddRepo("clean");
        harness.SimulateNoChangesFor("clean");

        var result = await harness.RunAsync();

        result.IsSuccess.Should().BeTrue();
        var opened = harness.Pipeline.Get<IReadOnlyList<OpenedPullRequest>>(ContextKeys.OpenedPullRequests);
        opened.Should().ContainSingle(o => o.RepoName == "changed" && o.Status == OpenStatus.Opened);
        opened.Should().ContainSingle(o => o.RepoName == "clean" && o.Status == OpenStatus.SkippedNoChanges);
    }

    [Fact]
    public async Task CommitAndPR_PublishesOpenedPullRequests_ListInPipelineContext()
    {
        var harness = new CommitAndPRHarness();
        harness.AddRepo("a");
        harness.AddRepo("b");

        await harness.RunAsync();

        var opened = harness.Pipeline.Get<IReadOnlyList<OpenedPullRequest>>(ContextKeys.OpenedPullRequests);
        opened.Should().HaveCount(2);
        opened.Should().AllSatisfy(o => o.Status.Should().Be(OpenStatus.Opened));
    }

    [Fact]
    public async Task Monorepo_WorkdirLayout_IsRunRoot_PlusOneRepoSubdir_NotFlat()
    {
        var harness = new CheckoutHarness();
        harness.AddRepo("solo", "https://x/solo.git");

        await harness.RunAsync();

        harness.CloneWorkdirs.Should().ContainSingle().Which.Should().Be("/work/solo");
    }

    private sealed class CheckoutHarness
    {
        public PipelineContext Pipeline { get; } = new();
        public List<string> CloneWorkdirs { get; } = new();
        public List<Step> CheckoutBranchSteps { get; } = new();

        private readonly List<RepoConnection> _repos = new();
        private readonly Mock<ISourceProviderFactory> _factoryMock = new();
        private readonly Mock<ISandbox> _sandboxMock = new();
        private readonly BranchName? _branch;

        public CheckoutHarness(string? branch = null)
        {
            _branch = branch is null ? null : new BranchName(branch);
            Pipeline.Set(ContextKeys.Sandbox, _sandboxMock.Object);
            _sandboxMock.Setup(s => s.RunStepAsync(
                    It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
                .Returns<Step, IProgress<StepEvent>?, CancellationToken>((step, _, _) =>
                {
                    if (step.Command == "git" && step.Args is { Count: > 0 } args)
                    {
                        if (args[0] == "-c" && args.Contains("clone") && step.WorkingDirectory is not null)
                            CloneWorkdirs.Add(step.WorkingDirectory);
                        else if (args[0] == "checkout" && args.Count == 2)
                            CheckoutBranchSteps.Add(step);
                    }
                    return Task.FromResult(new StepResult(StepResult.CurrentSchemaVersion, step.StepId, 0, false, 0.1, null));
                });
        }

        public void AddRepo(string name, string url)
        {
            _repos.Add(new RepoConnection { Name = name, Type = RepoType.GitHub, Url = url, DefaultBranch = "main" });
            var providerMock = new Mock<ISourceProvider>();
            providerMock.SetupGet(p => p.ProviderType).Returns("github");
            providerMock.Setup(p => p.CheckoutAsync(It.IsAny<BranchName?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Repository(new BranchName("main"), url));
            _factoryMock.Setup(f => f.Create(It.Is<RepoConnection>(r => r.Name == name)))
                .Returns(providerMock.Object);
        }

        public void AddLocalRepo(string name, string path)
        {
            _repos.Add(new RepoConnection { Name = name, Type = RepoType.Local, Path = path, DefaultBranch = "main" });
            var providerMock = new Mock<ISourceProvider>();
            providerMock.SetupGet(p => p.ProviderType).Returns("Local");
            providerMock.Setup(p => p.CheckoutAsync(It.IsAny<BranchName?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Repository(new BranchName("main"), path));
            _factoryMock.Setup(f => f.Create(It.Is<RepoConnection>(r => r.Name == name)))
                .Returns(providerMock.Object);
        }

        public Task<CommandResult> RunAsync()
        {
            Pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, _repos);
            var handler = new CheckoutSourceHandler(
                _factoryMock.Object,
                RunStateConceptsTestFactory.Default,
                NullLogger<CheckoutSourceHandler>.Instance);
            return handler.ExecuteAsync(new CheckoutSourceContext(_repos, _branch, Pipeline), CancellationToken.None);
        }
    }

    private sealed class CommitAndPRHarness
    {
        public PipelineContext Pipeline { get; } = new();
        private readonly List<RepoConnection> _repos = new();
        private readonly HashSet<string> _noChangeRepos = new(StringComparer.Ordinal);
        private readonly Mock<ISourceProviderFactory> _sourceFactoryMock = new();
        private readonly Mock<ITicketProviderFactory> _ticketFactoryMock = new();
        private readonly Mock<ISandbox> _sandboxMock = new();

        public CommitAndPRHarness()
        {
            _ticketFactoryMock.Setup(f => f.Create(It.IsAny<TrackerConnection>()))
                .Returns(new Mock<ITicketProvider>().Object);
            Pipeline.Set(ContextKeys.Sandbox, _sandboxMock.Object);
            _sandboxMock.Setup(s => s.RunStepAsync(
                    It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
                .Returns<Step, IProgress<StepEvent>?, CancellationToken>((step, _, _) =>
                {
                    if (step.Args is { Count: > 0 } args && args.Contains("commit") && step.WorkingDirectory is not null)
                    {
                        var repoName = step.WorkingDirectory.Replace("/work/", string.Empty);
                        if (_noChangeRepos.Contains(repoName))
                            return Task.FromResult(new StepResult(
                                StepResult.CurrentSchemaVersion, step.StepId, 1, false, 0.1,
                                "nothing to commit, working tree clean"));
                    }
                    return Task.FromResult(new StepResult(StepResult.CurrentSchemaVersion, step.StepId, 0, false, 0.1, null));
                });
        }

        public void AddRepo(string name)
        {
            _repos.Add(new RepoConnection { Name = name, Type = RepoType.GitHub, Url = $"https://x/{name}.git" });
            var providerMock = new Mock<ISourceProvider>();
            providerMock.Setup(p => p.CreatePullRequestAsync(
                    It.IsAny<Repository>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<CancellationToken>(), It.IsAny<TicketId?>()))
                .ReturnsAsync($"https://x/{name}/pull/1");
            _sourceFactoryMock.Setup(f => f.Create(It.Is<RepoConnection>(r => r.Name == name)))
                .Returns(providerMock.Object);
        }

        public void SimulateNoChangesFor(string repoName) => _noChangeRepos.Add(repoName);

        public Task<CommandResult> RunAsync()
        {
            Pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, _repos);
            var handler = new CommitAndPRHandler(
                _sourceFactoryMock.Object, _ticketFactoryMock.Object,
                new SandboxGitOperations(NullLogger<SandboxGitOperations>.Instance),
                NullLogger<CommitAndPRHandler>.Instance);
            var repository = new Repository(new BranchName("agent-smith/ticket-42"), "primary");
            var ticket = new Ticket(new TicketId("42"), "title", "desc", null, "Open", "GitHub");
            var changes = new List<CodeChange> { new(new FilePath("f.md"), "x", "Created") };
            return handler.ExecuteAsync(
                new CommitAndPRContext(repository, changes, ticket, _repos, new TrackerConnection(), Pipeline),
                CancellationToken.None);
        }
    }
}
