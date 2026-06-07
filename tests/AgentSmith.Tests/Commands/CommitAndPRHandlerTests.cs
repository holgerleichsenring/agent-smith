using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
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

public class CommitAndPRHandlerTests
{
    private readonly Mock<ISourceProviderFactory> _sourceFactoryMock = new();
    private readonly Mock<ITicketProviderFactory> _ticketFactoryMock = new();
    private readonly Mock<ISourceProvider> _sourceProviderMock = new();
    private readonly Mock<ITicketProvider> _ticketProviderMock = new();
    private readonly Mock<ISandbox> _sandboxMock = new();
    private readonly RecordingEventPublisher _events = new();
    private readonly CommitAndPRHandler _sut;

    public CommitAndPRHandlerTests()
    {
        _sourceFactoryMock.Setup(f => f.Create(It.IsAny<RepoConnection>()))
            .Returns(_sourceProviderMock.Object);
        _ticketFactoryMock.Setup(f => f.Create(It.IsAny<TrackerConnection>()))
            .Returns(_ticketProviderMock.Object);

        _sourceProviderMock.Setup(s => s.CreatePullRequestAsync(
                It.IsAny<Repository>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>(), It.IsAny<TicketId?>()))
            .ReturnsAsync("https://github.com/test/repo/pull/42");

        // p0228: the staged-diff probe (git diff --staged) must return a
        // non-empty diff so the handler knows there ARE changes to commit —
        // the context carries a README.md change. An empty diff now means
        // "nothing to commit" and the handler skips the commit on purpose
        // (see ExecuteAsync_NoStagedChanges_SkipsCommit_WithoutFailing).
        _sandboxMock.Setup(s => s.RunStepAsync(
                It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .Returns<Step, IProgress<StepEvent>?, CancellationToken>((step, _, _) =>
            {
                var output = step.Args is not null && step.Args.Contains("diff")
                    ? "+ staged change in README.md"
                    : (string?)null;
                return Task.FromResult(
                    new StepResult(StepResult.CurrentSchemaVersion, step.StepId, 0, false, 0.1, null, output));
            });

        _sut = new CommitAndPRHandler(
            _sourceFactoryMock.Object,
            _ticketFactoryMock.Object,
            new SandboxGitOperations(NullLogger<SandboxGitOperations>.Instance),
            new SecretPatternScanner(),
            _events,
            NullLogger<CommitAndPRHandler>.Instance);
    }

    [Fact]
    public async Task ExecuteAsync_CreatesCommitAndPR_ThenClosesTicket()
    {
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("pull/42");

        _sandboxMock.Verify(s => s.RunStepAsync(
            It.Is<Step>(st => st.Command == "git" && st.Args!.Contains("commit")),
            It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()), Times.Once);
        _sandboxMock.Verify(s => s.RunStepAsync(
            It.Is<Step>(st => st.Command == "git" && st.Args!.Contains("push")),
            It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()), Times.Once);

        _ticketProviderMock.Verify(t => t.FinalizeAsync(
            It.Is<TicketId>(id => id.Value == "123"),
            It.Is<string>(s => s.Contains("Agent Smith") && s.Contains("pull/42")),
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ForceStagesRunRecord_SoAGitignoredAgentsmithStillGetsAPR()
    {
        // p0234: the run record (.agentsmith/runs/{runId}/result.md) must always
        // be committable so every repo gets a PR — even if the repo .gitignores
        // .agentsmith. The handler force-stages it (`git add -f .agentsmith`).
        var context = CreateContext();

        await _sut.ExecuteAsync(context, CancellationToken.None);

        _sandboxMock.Verify(s => s.RunStepAsync(
            It.Is<Step>(st => st.Command == "git"
                && st.Args!.Contains("add") && st.Args!.Contains("-f") && st.Args!.Contains(".agentsmith")),
            It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task PullRequestOutcome_Event_IsEmittedPerRepo_WithStatusAndUrl()
    {
        var context = CreateContext();
        context.Pipeline.Set(ContextKeys.RunId, "run-x");

        await _sut.ExecuteAsync(context, CancellationToken.None);

        var outcome = _events.Events.OfType<PullRequestOutcomeEvent>().Single();
        outcome.Repo.Should().Be(context.Configs[0].Name);
        outcome.Status.Should().Be("opened");
        outcome.Url.Should().Contain("pull/42");
    }

    [Fact]
    public async Task ExecuteAsync_NoSandboxInPipelineContext_ReturnsFail()
    {
        var pipeline = new PipelineContext();
        var context = CreateContext(pipeline);

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("Sandboxes");
    }

    [Fact]
    public async Task ExecuteAsync_TicketCloseFailure_StillReturnsPRSuccess()
    {
        _ticketProviderMock.Setup(t => t.FinalizeAsync(
                It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("API error"));

        var context = CreateContext();
        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("pull/42");
    }

    [Fact]
    public async Task ExecuteAsync_IncludesChangeListInSummary()
    {
        string? postedSummary = null;
        _ticketProviderMock.Setup(t => t.FinalizeAsync(
                It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Callback<TicketId, string, string?, CancellationToken>((_, summary, _, _) => postedSummary = summary)
            .Returns(Task.CompletedTask);

        var context = CreateContext();
        await _sut.ExecuteAsync(context, CancellationToken.None);

        postedSummary.Should().NotBeNull();
        postedSummary.Should().Contain("README.md");
        postedSummary.Should().Contain("Created");
    }

    [Fact]
    public async Task ExecuteAsync_MultiRepo_SummaryListsEveryRepoNotJustPrimary()
    {
        // Multi-repo summary used to expose only the primary PR; operators
        // reading the ticket then had to traverse to the primary's body for
        // the sibling list (p0158c's cross-link). Listing each repo + its PR
        // status in the ticket comment keeps the ticket self-contained.
        string? postedSummary = null;
        _ticketProviderMock.Setup(t => t.FinalizeAsync(
                It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Callback<TicketId, string, string?, CancellationToken>((_, summary, _, _) => postedSummary = summary)
            .Returns(Task.CompletedTask);

        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.Sandbox, _sandboxMock.Object);
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes,
            new Dictionary<string, ISandbox>(StringComparer.Ordinal)
            {
                ["server"] = _sandboxMock.Object,
                ["client"] = _sandboxMock.Object,
                ["docs"] = _sandboxMock.Object,
            });
        var repo = new Repository(new BranchName("fix/123"), "https://github.com/test/repo");
        var ticket = new Ticket(new TicketId("123"), "Fix the bug", "Description", null, "Open", "GitHub");
        var changes = new List<CodeChange>
        {
            new(new FilePath("README.md"), "content", "Created")
        };
        var configs = new[]
        {
            new RepoConnection { Name = "server" },
            new RepoConnection { Name = "client" },
            new RepoConnection { Name = "docs" },
        };
        var context = new CommitAndPRContext(repo, changes, ticket, configs, new TrackerConnection(), pipeline);

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Message);
        _sourceProviderMock.Verify(s => s.CreatePullRequestAsync(
            It.IsAny<Repository>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>(), It.IsAny<TicketId?>()), Times.Exactly(3));
        postedSummary.Should().NotBeNull();
        postedSummary.Should().Contain("Pull requests");
        postedSummary.Should().Contain("**server**:");
        postedSummary.Should().Contain("**client**:");
        postedSummary.Should().Contain("**docs**:");
    }

    [Fact]
    public async Task ExecuteAsync_WithDoneStatus_FinalizesAtomicallyWithStatus()
    {
        var pipeline = NewPipelineWithSandbox();
        pipeline.Set(ContextKeys.DoneStatus, "In Review");
        var context = CreateContext(pipeline);

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        // Comment + status transition collapsed into ONE provider call so AzDO
        // can emit a single PATCH and avoid the TF26071 rev race.
        _ticketProviderMock.Verify(t => t.FinalizeAsync(
            It.Is<TicketId>(id => id.Value == "123"),
            It.Is<string>(s => s.Contains("Agent Smith")),
            "In Review",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_StagedDiffContainsSecretPattern_AbortsBeforeCommit()
    {
        // p0192: defence-in-depth gate. Staged diff carries a ghp_ PAT-shaped
        // string → scanner matches → commit must not run; no PR opened.
        _sandboxMock.Reset();
        _sandboxMock.Setup(s => s.RunStepAsync(
                It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .Returns<Step, IProgress<StepEvent>?, CancellationToken>((step, _, _) =>
            {
                var output = step.Args is not null && step.Args.Contains("diff")
                    ? "+token=ghp_abcdefghijklmnop1234567"
                    : (string?)null;
                return Task.FromResult(
                    new StepResult(StepResult.CurrentSchemaVersion, step.StepId, 0, false, 0.1, null, output));
            });

        var context = CreateContext();
        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        _sandboxMock.Verify(s => s.RunStepAsync(
            It.Is<Step>(st => st.Command == "git" && st.Args!.Contains("commit")),
            It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()), Times.Never);
        _sourceProviderMock.Verify(s => s.CreatePullRequestAsync(
            It.IsAny<Repository>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>(), It.IsAny<TicketId?>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_NoStagedChanges_SkipsCommit_WithoutFailing()
    {
        // p0228: a repo the agent never touched has nothing to commit. Running
        // `git commit` anyway exits 1 ("nothing to commit") — a red, alarming
        // sandbox row for a normal outcome. The handler now probes the staged
        // diff first and skips the doomed commit (same as p0226's persist
        // guard), emitting a neutral no_changes outcome.
        _sandboxMock.Reset();
        _sandboxMock.Setup(s => s.RunStepAsync(
                It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .Returns<Step, IProgress<StepEvent>?, CancellationToken>((step, _, _) =>
                // empty diff output → nothing staged → nothing to commit
                Task.FromResult(new StepResult(StepResult.CurrentSchemaVersion, step.StepId, 0, false, 0.1, null)));

        var pipeline = NewPipelineWithSandbox();
        pipeline.Set(ContextKeys.RunId, "run-x");
        var context = CreateContext(pipeline);

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        // the doomed `git commit` must never run, and no PR is opened
        _sandboxMock.Verify(s => s.RunStepAsync(
            It.Is<Step>(st => st.Command == "git" && st.Args!.Contains("commit")),
            It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()), Times.Never);
        _sourceProviderMock.Verify(s => s.CreatePullRequestAsync(
            It.IsAny<Repository>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>(), It.IsAny<TicketId?>()), Times.Never);

        // and the per-repo outcome is the neutral "no changes", not a failure
        var outcome = _events.Events.OfType<PullRequestOutcomeEvent>().Single();
        outcome.Status.Should().Be("no_changes");
        result.IsSuccess.Should().BeTrue(result.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutDoneStatus_FinalizesAtomicallyWithNullStatus()
    {
        var context = CreateContext();

        await _sut.ExecuteAsync(context, CancellationToken.None);

        _ticketProviderMock.Verify(t => t.FinalizeAsync(
            It.IsAny<TicketId>(),
            It.IsAny<string>(),
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_FixBugPreset_NoCodeChange_FailsAndDoesNotResolveTicket()
    {
        // p0241 keystone: a fix-bug run that staged nothing real is a FAILURE,
        // not a hollow "success" — and it must not mark the ticket resolved.
        _sandboxMock.Reset();
        _sandboxMock.Setup(s => s.RunStepAsync(
                It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .Returns<Step, IProgress<StepEvent>?, CancellationToken>((step, _, _) =>
                Task.FromResult(new StepResult(StepResult.CurrentSchemaVersion, step.StepId, 0, false, 0.1, null)));

        var pipeline = NewPipelineWithSandbox();
        pipeline.Set(ContextKeys.PipelineName, "fix-bug");
        // Truly zero changes: empty CodeChanges AND empty staged diff (sandbox
        // reset above) — the no-code-change branch of the keystone.
        var context = CreateContext(pipeline) with { Changes = Array.Empty<CodeChange>() };

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("no code changes");
        _ticketProviderMock.Verify(t => t.FinalizeAsync(
            It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_FixBugPreset_WithChangeAndGreenVerdict_SucceedsAndResolvesTicket()
    {
        // p0241 keystone: real diff + a green verification verdict → the happy
        // path still succeeds and resolves the ticket with the keystone active.
        var pipeline = NewPipelineWithSandbox();
        pipeline.Set(ContextKeys.PipelineName, "fix-bug");
        pipeline.Set(ContextKeys.MasterVerification,
            new MasterVerification(VerificationStatus.Green, true, true, true, true, "fixed"));
        var context = CreateContext(pipeline);

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Message.Should().Contain("pull/42");
        _ticketProviderMock.Verify(t => t.FinalizeAsync(
            It.Is<TicketId>(id => id.Value == "123"), It.IsAny<string>(), It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_FixBugPreset_WithChangeButNoVerdict_Fails()
    {
        // p0241 keystone: code changed but the agent emitted no verdict → the
        // build/test outcome is unknown, so the run cannot be a success.
        var pipeline = NewPipelineWithSandbox();
        pipeline.Set(ContextKeys.PipelineName, "fix-bug");
        var context = CreateContext(pipeline);

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("verification verdict");
    }

    private CommitAndPRContext CreateContext(PipelineContext? pipeline = null)
    {
        var pl = pipeline ?? NewPipelineWithSandbox();
        var repo = new Repository(new BranchName("fix/123"), "https://github.com/test/repo");
        var ticket = new Ticket(new TicketId("123"), "Fix the bug", "Description", null, "Open", "GitHub");
        var changes = new List<CodeChange>
        {
            new(new FilePath("README.md"), "content", "Created")
        };
        return new CommitAndPRContext(repo, changes, ticket, new[] { new RepoConnection() }, new TrackerConnection(), pl);
    }

    private void SeedSandboxes(PipelineContext pipeline)
    {
        pipeline.Set(ContextKeys.Sandbox, _sandboxMock.Object);
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes,
            new Dictionary<string, ISandbox>(StringComparer.Ordinal) { [string.Empty] = _sandboxMock.Object });
    }

    private PipelineContext NewPipelineWithSandbox()
    {
        var pipeline = new PipelineContext();
        SeedSandboxes(pipeline);
        return pipeline;
    }
}
