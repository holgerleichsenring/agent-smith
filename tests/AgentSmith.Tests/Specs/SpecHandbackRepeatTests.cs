using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Persistence;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Triage;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Persistence;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-09-07-bd7a: a contradiction handed back twice with nothing said in between ends
/// the loop. The old guard compared the sha recorded at the last park with the branch
/// head — and every derivation commits a fresh timestamped revision, so the two never
/// matched and the loop never ended. The producer test here runs the REAL publish path
/// (publisher, writer, git operations, index) over a sandbox that models git, twice; it
/// was red before the fix, with the second hand-back parking again.
/// </summary>
public sealed class SpecHandbackRepeatTests
{
    private static readonly RepoConnection Repo = new() { Name = "primary" };
    private static readonly Ticket Ticket = new(new TicketId("1"), "t", "d", null, "open", "azdo", []);

    [Fact]
    public async Task Contradiction_PublishedTwiceThroughTheRealWriter_TheSecondHandbackEndsTheLoop()
    {
        var thread = new List<TicketComment>();
        var tickets = ParkingProvider(thread);
        var files = new RecordingFileReader();
        var git = new GitModellingSandbox(files);
        var pointers = new InMemorySpecSetPointerStore();
        var publisher = Publisher(files, git, pointers);
        var handler = Handler(tickets, pointers);

        var first = Finalize(Contradiction(), previous: null);
        var parked = await RunAsync(publisher, handler, git, thread, first);
        var again = await RunAsync(publisher, handler, git, thread, Finalize(Contradiction(), first));

        parked.Message.Should().Contain("awaiting_user_input");
        git.Commits.Should().Be(2, "every derivation commits a fresh timestamped revision");
        again.Message.Should().Contain("the loop ends here", "nobody said anything since the last hand-back");
        again.IsSuccess.Should().BeFalse("the loop ends as a failed step, never as a completion");
        tickets.Verify(t => t.FinalizeAsync(
            It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // One run: publish the finalized set the way DeriveSpec does, then hand back.
    private static async Task<CommandResult> RunAsync(
        SpecSetPublisher publisher, SpecHandbackHandler handler, ISandbox sandbox,
        IReadOnlyList<TicketComment> thread, SpecSet set)
    {
        var pipeline = Pipeline(sandbox, [.. thread]);
        await publisher.PublishAsync(pipeline, string.Empty, Repo, set, [], default);
        return await handler.ExecuteAsync(
            new SpecHandbackContext(Ticket, Parkable(), [Repo], pipeline), default);
    }

    // DeriveSpecHandler.Finalize: the revision header is ours, stamped now.
    private static SpecSet Finalize(SpecSet set, SpecSet? previous)
    {
        var history = previous?.Revisions ?? [];
        var next = new SpecRevision(history.Count + 1, SpecRevisionCause.Initial, DateTimeOffset.UtcNow);
        return set with { Revisions = [.. history, next] };
    }

    private static SpecSet Contradiction() => new(
        SpecSetKey.For("azdo", "1").Value, [], SpecAccounting.Empty, [], SpecSource.Derived,
        new SpecHandback(SpecHandbackCase.RequirementsContradictRepository, "no such client here"));

    private static Mock<ITicketProvider> ParkingProvider(List<TicketComment> thread)
    {
        var tickets = new Mock<ITicketProvider>();
        tickets.Setup(t => t.FinalizeAsync(
                It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<TicketId, string, string, CancellationToken>((_, comment, _, _) =>
                thread.Add(new TicketComment("agent-smith", DateTimeOffset.UtcNow, comment)))
            .Returns(Task.CompletedTask);
        return tickets;
    }

    private static SpecSetPublisher Publisher(
        ISandboxFileReader files, ISandbox sandbox, ISpecSetPointerStore pointers)
    {
        var readers = new Mock<ISandboxFileReaderFactory>();
        readers.Setup(f => f.Create(sandbox)).Returns(files);
        var writer = new SpecSetWriter(
            readers.Object,
            new SandboxGitOperations(new GitBranchPusher(), NullLogger<SandboxGitOperations>.Instance,
                readers.Object, new SandboxGitIdentity(NullLogger<SandboxGitIdentity>.Instance)),
            new SpecSetIndex(), new SandboxTargets(), NullLogger<SpecSetWriter>.Instance);
        return new SpecSetPublisher(
            writer, new SpecSetPointerRecorder(pointers, NullLogger<SpecSetPointerRecorder>.Instance),
            Mock.Of<ISpecPullRequestOpener>(),
            new SpecRefusalReporter(Mock.Of<IEventPublisher>(), NullLogger<SpecRefusalReporter>.Instance),
            Mock.Of<IRunArtifactStore>(), NullLogger<SpecSetPublisher>.Instance);
    }

    private static SpecHandbackHandler Handler(Mock<ITicketProvider> tickets, ISpecSetPointerStore pointers)
    {
        var factory = new Mock<ITicketProviderFactory>();
        factory.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(tickets.Object);
        return new SpecHandbackHandler(
            factory.Object,
            new SpecParkStatusResolver(new ClarificationParkStatusResolver()),
            pointers,
            new SpecHandbackRepeat(NullLogger<SpecHandbackRepeat>.Instance),
            NullLogger<SpecHandbackHandler>.Instance);
    }

    private static TrackerConnection Parkable() => new()
    {
        Type = TrackerType.AzureDevOps,
        NeedsClarificationStatus = "needs-info",
        NotImplementableStatus = "blocked",
    };

    private static PipelineContext Pipeline(ISandbox sandbox, IReadOnlyList<TicketComment> thread)
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes, new Dictionary<string, ISandbox> { ["primary"] = sandbox });
        pipeline.Set(ContextKeys.Repository,
            new Repository(new BranchName("agent-smith/1"), "https://example.test/repo.git"));
        pipeline.Set(ContextKeys.TicketComments, thread);
        return pipeline;
    }

    /// <summary>
    /// Git as the writer sees it: `add` snapshots the files written so far, `diff --cached
    /// --quiet` exits 1 while the snapshot differs from the last commit, `commit` takes the
    /// snapshot and moves HEAD, `rev-parse HEAD` names the commit. Everything else exits 0.
    /// </summary>
    private sealed class GitModellingSandbox(RecordingFileReader files) : ISandbox
    {
        private Dictionary<string, string> _staged = new(StringComparer.Ordinal);
        private Dictionary<string, string> _committed = new(StringComparer.Ordinal);

        public string JobId => "git-model";
        public int Commits { get; private set; }

        public Task<StepResult> RunStepAsync(Step step, IProgress<StepEvent>? progress, CancellationToken ct)
        {
            var exit = 0;
            var output = string.Empty;
            switch (step.Args)
            {
                case ["add", ..]:
                    _staged = new Dictionary<string, string>(files.Written, StringComparer.Ordinal);
                    break;
                case ["diff", "--cached", "--quiet"]:
                    exit = StagedMatchesCommitted() ? 0 : 1;
                    break;
                case ["commit", ..]:
                    _committed = _staged;
                    Commits++;
                    break;
                case ["rev-parse", "HEAD"]:
                    output = $"sha-{Commits}";
                    break;
            }
            return Task.FromResult(new StepResult(
                StepResult.CurrentSchemaVersion, step.StepId, exit, false, 0, null, output));
        }

        private bool StagedMatchesCommitted() =>
            _staged.Count == _committed.Count
            && _staged.All(kv => _committed.TryGetValue(kv.Key, out var v) && v == kv.Value);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class RecordingFileReader : ISandboxFileReader
    {
        public Dictionary<string, string> Written { get; } = new(StringComparer.Ordinal);

        public Task<bool> ExistsAsync(string path, CancellationToken ct) => Task.FromResult(false);
        public Task<string?> TryReadAsync(string path, CancellationToken ct) => Task.FromResult<string?>(null);
        public Task<string> ReadRequiredAsync(string path, CancellationToken ct) => Task.FromResult(string.Empty);

        public Task<IReadOnlyList<string>> ListAsync(string path, int? maxDepth, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<string>>([.. Written.Keys]);

        public Task WriteAsync(string path, string content, CancellationToken ct)
        {
            Written[path] = content;
            return Task.CompletedTask;
        }
    }
}
