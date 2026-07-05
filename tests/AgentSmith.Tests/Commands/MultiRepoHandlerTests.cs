using AgentSmith.Contracts.Services;
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
/// p0158e spec-driven behaviour: per-repo sandboxes, handlers dispatch git ops
/// to the right sandbox, single-repo monorepo path still works.
/// </summary>
public sealed class MultiRepoHandlerTests
{
    [Fact]
    public async Task Checkout_PerRepo_DispatchesCloneToOwnSandbox()
    {
        var harness = new CheckoutHarness();
        harness.AddRepo("server", "https://x/server.git");
        harness.AddRepo("client", "https://x/client.git");

        var result = await harness.RunAsync();

        result.IsSuccess.Should().BeTrue();
        harness.GetSandbox("server").Verify(s => s.RunStepAsync(
            It.Is<Step>(st => st.Command == "git" && st.Args!.Contains("clone")
                && st.Args!.Contains("https://x/server.git")),
            It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()), Times.Once);
        harness.GetSandbox("client").Verify(s => s.RunStepAsync(
            It.Is<Step>(st => st.Command == "git" && st.Args!.Contains("clone")
                && st.Args!.Contains("https://x/client.git")),
            It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Checkout_LocalProvider_PublishesPrimaryRepository_AtConstWorkPath()
    {
        var harness = new CheckoutHarness();
        harness.AddLocalRepo("only-repo", "/tmp");

        var result = await harness.RunAsync();

        result.IsSuccess.Should().BeTrue();
        var stored = harness.Pipeline.Get<Repository>(ContextKeys.Repository);
        stored.LocalPath.Should().Be(Repository.SandboxWorkPath);
    }

    [Fact]
    public async Task BranchCreate_SameName_AppliedInEveryRepoSandbox()
    {
        var harness = new CheckoutHarness(branch: "agent-smith/ticket-42");
        harness.AddRepo("a", "https://x/a.git");
        harness.AddRepo("b", "https://x/b.git");

        await harness.RunAsync();

        foreach (var name in new[] { "a", "b" })
            harness.GetSandbox(name).Verify(s => s.RunStepAsync(
                It.Is<Step>(st => st.Command == "git" && st.Args!.Count == 2
                    && st.Args!.Contains("checkout") && st.Args!.Contains("agent-smith/ticket-42")),
                It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()), Times.Once);
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
    public async Task Monorepo_N1_StillWorks_PrimaryRepositoryPublishedAtConstWorkPath()
    {
        var harness = new CheckoutHarness();
        harness.AddRepo("solo", "https://x/solo.git");

        var result = await harness.RunAsync();

        result.IsSuccess.Should().BeTrue();
        var stored = harness.Pipeline.Get<Repository>(ContextKeys.Repository);
        stored.LocalPath.Should().Be(Repository.SandboxWorkPath);
        harness.GetSandbox("solo").Verify(s => s.RunStepAsync(
            It.Is<Step>(st => st.Command == "git" && st.Args!.Contains("clone")),
            It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private sealed class CheckoutHarness
    {
        public PipelineContext Pipeline { get; } = new();

        private readonly List<RepoConnection> _repos = new();
        private readonly Dictionary<string, Mock<ISandbox>> _sandboxes = new(StringComparer.Ordinal);
        private readonly Mock<ISourceProviderFactory> _factoryMock = new();
        private readonly BranchName? _branch;

        public CheckoutHarness(string? branch = null)
        {
            _branch = branch is null ? null : new BranchName(branch);
        }

        public Mock<ISandbox> GetSandbox(string repoName) => _sandboxes[repoName];

        public void AddRepo(string name, string url)
        {
            _repos.Add(new RepoConnection { Name = name, Type = RepoType.GitHub, Url = url, DefaultBranch = "main" });
            RegisterProvider(name, url, providerType: "github");
            RegisterSandbox(name);
        }

        public void AddLocalRepo(string name, string path)
        {
            _repos.Add(new RepoConnection { Name = name, Type = RepoType.Local, Path = path, DefaultBranch = "main" });
            RegisterProvider(name, path, providerType: "Local");
            RegisterSandbox(name);
        }

        private void RegisterProvider(string name, string url, string providerType)
        {
            var providerMock = new Mock<ISourceProvider>();
            providerMock.SetupGet(p => p.ProviderType).Returns(providerType);
            providerMock.Setup(p => p.CheckoutAsync(It.IsAny<BranchName?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Repository(new BranchName("main"), url));
            _factoryMock.Setup(f => f.Create(It.Is<RepoConnection>(r => r.Name == name)))
                .Returns(providerMock.Object);
        }

        private void RegisterSandbox(string name)
        {
            var sandboxMock = new Mock<ISandbox>();
            sandboxMock.Setup(s => s.RunStepAsync(
                    It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
                .Returns<Step, IProgress<StepEvent>?, CancellationToken>((step, _, _) =>
                    Task.FromResult(new StepResult(StepResult.CurrentSchemaVersion, step.StepId, 0, false, 0.1, null)));
            _sandboxes[name] = sandboxMock;
        }

        public Task<CommandResult> RunAsync()
        {
            Pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, _repos);
            Pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(
                ContextKeys.Sandboxes,
                _sandboxes.ToDictionary(kv => kv.Key, kv => kv.Value.Object, StringComparer.Ordinal));
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
        private readonly Dictionary<string, Mock<ISandbox>> _sandboxes = new(StringComparer.Ordinal);
        private readonly HashSet<string> _noChangeRepos = new(StringComparer.Ordinal);
        private readonly Mock<ISourceProviderFactory> _sourceFactoryMock = new();
        private readonly Mock<ITicketProviderFactory> _ticketFactoryMock = new();

        public CommitAndPRHarness()
        {
            _ticketFactoryMock.Setup(f => f.Create(It.IsAny<TrackerConnection>()))
                .Returns(new Mock<ITicketProvider>().Object);
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

            var capturedName = name;
            var sandboxMock = new Mock<ISandbox>();
            sandboxMock.Setup(s => s.RunStepAsync(
                    It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
                .Returns<Step, IProgress<StepEvent>?, CancellationToken>((step, _, _) =>
                {
                    // p0228: the handler now decides "nothing to commit" from the
                    // STAGED DIFF probe before running git commit (so the doomed
                    // commit never produces a red exit-1 row). Model a clean repo
                    // as an empty staged diff; a changed repo as a non-empty one.
                    if (step.Args is { Count: > 0 } args && args.Contains("diff"))
                    {
                        var diff = _noChangeRepos.Contains(capturedName)
                            ? null
                            : $"+ change in {capturedName}";
                        return Task.FromResult(new StepResult(
                            StepResult.CurrentSchemaVersion, step.StepId, 0, false, 0.1, null, diff));
                    }
                    return Task.FromResult(new StepResult(StepResult.CurrentSchemaVersion, step.StepId, 0, false, 0.1, null));
                });
            _sandboxes[name] = sandboxMock;
        }

        public void SimulateNoChangesFor(string repoName) => _noChangeRepos.Add(repoName);

        public Task<CommandResult> RunAsync()
        {
            Pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, _repos);
            Pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(
                ContextKeys.Sandboxes,
                _sandboxes.ToDictionary(kv => kv.Key, kv => kv.Value.Object, StringComparer.Ordinal));
            var handler = new CommitAndPRHandler(
                _sourceFactoryMock.Object, _ticketFactoryMock.Object,
                new SandboxGitOperations(NullLogger<SandboxGitOperations>.Instance, new StubSandboxFileReaderFactory()),
                new SecretPatternScanner(),
                EventTestStubs.NoOp,
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
