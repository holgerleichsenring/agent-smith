using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Lifecycle;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Tests.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Commands;

/// <summary>
/// 2026-10-12-3a36c: CommitAndPR over several repositories, each with its own sandbox that
/// answers <c>git diff --cached --name-only</c> with the paths the test staged there and
/// records every step it ran — so a test reads which repository got a commit, a push and a PR.
/// </summary>
internal sealed class RepoCommitFixture
{
    public const string Branch = "agent-smith/42";
    public const string ResultMd = ".agentsmith/runs/run-1/result.md";

    private readonly List<RepoConnection> _repos = [];
    private readonly Dictionary<string, List<Step>> _ran = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ISandbox> _sandboxes = new(StringComparer.Ordinal);

    public Mock<ISourceProvider> Provider { get; } = new();
    public PipelineContext Pipeline { get; } = new();
    public RecordingEventPublisher Events { get; } = new();

    public RepoCommitFixture()
    {
        Provider.Setup(p => p.CreatePullRequestAsync(
                It.IsAny<Repository>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(),
                It.IsAny<TicketId?>(), It.IsAny<bool>(), It.IsAny<BranchName?>()))
            .ReturnsAsync((Repository r, string _, string _, CancellationToken _, TicketId? _, bool _, BranchName? _)
                => $"{r.RemoteUrl}/pull/1");
        Provider.Setup(p => p.UpdatePullRequestBodyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        Pipeline.Set(ContextKeys.RunId, "run-1");
    }

    public RepoCommitFixture AddRepo(string name, params string[] staged)
    {
        _repos.Add(new RepoConnection { Name = name, Url = $"https://stub.test/{name}" });
        var ran = new List<Step>();
        _ran[name] = ran;
        var sandbox = new Mock<ISandbox>();
        sandbox.Setup(s => s.RunStepAsync(It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .Returns<Step, IProgress<StepEvent>?, CancellationToken>((step, _, _) =>
            {
                ran.Add(step);
                return Task.FromResult(new StepResult(
                    StepResult.CurrentSchemaVersion, step.StepId, 0, false, 0.1, null, Answer(step, staged)));
            });
        _sandboxes[name] = sandbox.Object;
        return this;
    }

    public IReadOnlyList<Step> RanIn(string repo) => _ran[repo];

    public IEnumerable<string> CommitMessagesIn(string repo) => _ran[repo]
        .Where(s => s.Command == "git" && s.Args is { } a && a.Contains("commit"))
        .Select(s => s.Args!.SkipWhile(a => a != "-m").ElementAt(1));

    public bool PushedIn(string repo) =>
        _ran[repo].Any(s => s.Command == "git" && s.Args is { } a && a.Contains("push"));

    public Task<CommandResult> RunAsync()
    {
        Pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, _repos);
        Pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes, _sandboxes);
        Pipeline.Set<IReadOnlyDictionary<string, string>>(
            ContextKeys.SandboxRepos, _sandboxes.Keys.ToDictionary(k => k, k => k, StringComparer.Ordinal));
        var ticket = new Ticket(new TicketId("42"), "Fix the bug", "desc", null, "Open", "GitHub");
        return BuildHandler().ExecuteAsync(
            new CommitAndPRContext(new Repository(new BranchName(Branch), "primary"),
                [new CodeChange(new FilePath("src/a.cs"), "x", "Modified")], ticket, _repos,
                new TrackerConnection(), Pipeline),
            CancellationToken.None);
    }

    private static string? Answer(Step step, IReadOnlyList<string> staged)
    {
        if (step.Command != "git" || step.Args is not { } args || !args.Contains("diff")) return null;
        if (args.Contains("--name-only")) return string.Join("\n", staged);
        return staged.Count == 0 ? null : string.Concat(staged.Select(f => $"+ change in {f}\n"));
    }

    private CommitAndPRHandler BuildHandler()
    {
        var factory = new Mock<ISourceProviderFactory>();
        factory.Setup(f => f.Create(It.IsAny<RepoConnection>())).Returns(Provider.Object);
        var gitOps = GitOps();
        return new CommitAndPRHandler(
            factory.Object, new Mock<ITicketProviderFactory>().Object, gitOps, new SecretPatternScanner(), new PullRequestOutcomePublisher(Events),
            new TicketLifecycle(), new SandboxTargets(),
            new SpecDeliveryAccounting(TestGit.Delivery,
                new SpecAccountant(new ScriptedChatClientFactory(),
                    new AccountCalls(new SpecAccountCall(new ScriptedChatClientFactory(),
                        new Application.Services.Events.AsyncLocalRunContextAccessor(), NullLogger<SpecAccountCall>.Instance)),
                    NullLogger<SpecAccountant>.Instance),
                new SandboxTargets(), NullLogger<SpecDeliveryAccounting>.Instance),
            new FailedRunPersistence(new WipCommitMessage()),
            new SpecOnlyPullRequests(factory.Object, new PullRequestOutcomePublisher(Events), NullLogger<SpecOnlyPullRequests>.Instance),
            new ShortfallDelivery(new UnverifiedWorkReverter(gitOps, NullLogger<UnverifiedWorkReverter>.Instance),
                NullLogger<ShortfallDelivery>.Instance),
            new CompletedRunTicketSummary(),
            AgentSmith.Tests.Structure.StructureGateFixture.Publisher(),
            NullLogger<CommitAndPRHandler>.Instance);
    }

    private static SandboxGitOperations GitOps() =>
        new(new GitBranchPusher(), TestSupport.TestGitCredentials.Resolver, NullLogger<SandboxGitOperations>.Instance,
            new StubSandboxFileReaderFactory(), new SandboxGitIdentity(NullLogger<SandboxGitIdentity>.Instance));
}
