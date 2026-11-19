using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Events;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.SpecExecution;
using AgentSmith.Application.Services.Persistence;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Validation;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-10-08-f114: a set whose every spec ran — the branch carries both specs under done/, as a
/// branch re-cut from a base that merged the series does — appends a stand-in for a comment or a
/// Request changes (2026-11-19-6b0ba), its executed head kept.
/// </summary>
public sealed class DeriveSpecAppendTests
{
    private const string Key = "azdo-19106";
    private const string Series = "2026-10-06-0a0a";
    private static readonly string HeadId = SeriesIdFactory.Member(Series, 0);
    private static readonly string TailId = SeriesIdFactory.Member(Series, 1);
    private static readonly string NewId = SeriesIdFactory.Member(Series, 2);
    private const string MarkerSha = "marker-sha-2";
    private const string TicketText = """
        Migrate the client.

        Ping me if unclear.
        """;

    [Fact]
    public async Task Derive_FullyExecutedSetWithComment_AppendsAStandInAfterHead()
    {
        var published = new CapturingPublisher();

        await Handler(published, pointerSha: MarkerSha)
            .ExecuteAsync(Context([OurCut(), Operator("rename the client as well")]), default);

        published.Set!.Specs.Select(p => p.SpecId).Should().Equal(HeadId, TailId, NewId);
        published.Set.UnexecutedTail.Select(p => p.SpecId).Should().Equal(NewId);
        published.Set.StandIns.Should().Equal([NewId], "its coding master writes the spec the comment asks for");
        published.Set.Current.Cause.Should().Be(SpecRevisionCause.Comment);
    }

    [Fact]
    public async Task Derive_BranchFromBaseCarryingMergedSeries_ReworkAppendsAStandIn()
    {
        var published = new CapturingPublisher();

        await Handler(published, pointerSha: MarkerSha)
            .ExecuteAsync(Context([OurCut()], new ReworkAct("alice", DateTimeOffset.UtcNow, ReworkChannel.PullRequest)), default);

        published.Set!.UnexecutedTail.Select(p => p.SpecId).Should().Equal(NewId);
        published.Set.Executed.Should().Equal([HeadId, TailId], "the executed head is kept as it ran");
        published.Set.Current.Cause.Should().Be(SpecRevisionCause.Rework);
    }

    private static DeriveSpecHandler Handler(ISpecSetPublisher publisher, string? pointerSha)
    {
        var files = SeededFiles();
        var factory = new Mock<ISandboxFileReaderFactory>();
        factory.Setup(f => f.Create(It.IsAny<ISandbox>())).Returns(files);
        var gitOps = new SandboxGitOperations(
            new GitBranchPusher(), AgentSmith.Tests.TestSupport.TestGitCredentials.Resolver, NullLogger<SandboxGitOperations>.Instance, factory.Object,
            new SandboxGitIdentity(NullLogger<SandboxGitIdentity>.Instance));
        var draftReader = new SpecDraftReader();
        var reader = AgentSmith.Tests.TestSupport.SeriesDoubles.Reader(factory.Object, gitOps);
        // The last commit on the spec path is the marker's; the pointer names what the caller says.
        var pointers = new InMemorySpecSetPointerStore();
        if (pointerSha is not null)
            pointers.SaveAsync(string.Empty, new SpecSetPointer(Key, "primary", pointerSha, 1), default)
                .GetAwaiter().GetResult();
        var validator = new SpecDraftValidator(new SpecSchemaProvider());
        var tickets = new Mock<ITicketProviderFactory>();
        return new DeriveSpecHandler(
            SeriesDoubles.BranchRead(reader), publisher, pointers,
            new ApprovedSpecSetResolver(
                new InMemorySpecApprovalStore(), NullLogger<ApprovedSpecSetResolver>.Instance),
            new SpecSourceResolver(
                new ApprovedSetHandoff(NullLogger<ApprovedSetHandoff>.Instance), AgentSmith.Tests.TestSupport.ApprovedSetDoubles.Continuation(),
                new FiledTicketSpecGate(NullLogger<FiledTicketSpecGate>.Instance),
                AgentSmith.Tests.TestSupport.SeriesSettingsDoubles.Reader(), NullLogger<SpecSourceResolver>.Instance),
            new SpecStandIns(validator, draftReader, new DerivedSpecYamlRenderer()),
            new SpecSetTicketCommenter(tickets.Object, Moq.Mock.Of<AgentSmith.Contracts.Reviews.IFullSetPrNotice>(), AgentSmith.Tests.TestSupport.SeriesSettingsDoubles.Reader(), NullLogger<SpecSetTicketCommenter>.Instance),
            ApprovedSetDoubles.KeptNotice(),
            new UnansweredQuestionPin(NullLogger<UnansweredQuestionPin>.Instance),
            new UnansweredQuestionNotice(tickets.Object, NullLogger<UnansweredQuestionNotice>.Instance),
            new SeriesResolver(new SeriesIdFactory(TimeProvider.System)),
            AgentSmith.Tests.TestSupport.HandbackTestDoubles.Probe(),
            new SpecSetUnchanged(new SeriesFiles(new SeriesManifest())));
    }

    private static DeriveSpecContext Context(IReadOnlyList<TicketComment> thread, ReworkAct? act = null)
    {
        var sandbox = new Mock<ISandbox>();
        sandbox.Setup(s => s.RunStepAsync(It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StepResult(1, Guid.Empty, 0, false, 0, null, MarkerSha));
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.RunId, "run-2");
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes, new Dictionary<string, ISandbox> { ["primary"] = sandbox.Object });
        pipeline.Set(ContextKeys.Ticket, Ticket());
        pipeline.Set(ContextKeys.TicketComments, thread);
        if (act is not null) pipeline.Set(ContextKeys.ReworkAct, act);
        return new DeriveSpecContext(
            Ticket(), null, [new RepoConnection { Name = "primary" }], new AgentConfig(), pipeline);
    }

    private static Ticket Ticket() =>
        new(new TicketId("19106"), "Migrate the client", TicketText, null, "open", "azdo", []);

    private static TicketComment OurCut() => new(
        "agent-smith", DateTimeOffset.UtcNow.AddHours(-2),
        SpecSetComment.Render(Head(), null, AgentSmith.Tests.TestSupport.SeriesSettingsDoubles.Defaults));

    private static TicketComment Operator(string body) =>
        new("operator", DateTimeOffset.UtcNow.AddHours(-1), body);

    private static SeededFileReader SeededFiles()
    {
        var files = new SeededFileReader();
        files.Seed($".agentsmith/series/{Series}.yaml", SetYaml(TicketTextFingerprint.Of(Ticket())));
        files.Seed($".agentsmith/specs/done/{HeadId}-first.yaml", SpecYaml(HeadId));
        files.Seed($".agentsmith/specs/done/{TailId}-second.yaml", SpecYaml(TailId));
        return files;
    }

    private static string SetYaml(string fingerprint) => $"""
        ticket: {Key}
        specs:
        - {HeadId}
        - {TailId}
        revisions:
        - number: 1
          cause: initial derivation
          at: 2026-09-08T10:00:00.0000000+00:00
        carried:
        - segment: 1
          phase: {HeadId}
        - segment: 2
          phase: {TailId}
        ticket_fingerprint: {fingerprint}
        """;

    private static string SpecYaml(string id) => $"""
        spec: {id}
        goal: "Goal {id}"
        done:
          - "Done {id}."
        """;

    // The set the branch carries, both specs executed — what our cut comment rendered.
    private static SpecSet Head()
    {
        var segments = TicketSegmenter.Segment(TicketText);
        var carries = segments.Select(s => s.Id).ToList();
        DerivedSpec Spec(string id) => new(
            new SpecDraft(id, $"Goal {id}", $"spec: {id}\ngoal: \"Goal {id}\"", []) { Done = [$"Done {id}."] },
            id, string.Empty, carries);
        var specs = new[] { Spec(HeadId), Spec(TailId) };
        return new SpecSet(
            Key, specs, SpecAccountingBuilder.Build(specs, [], segments),
            [new SpecRevision(1, SpecRevisionCause.Initial, DateTimeOffset.UtcNow)],
            SpecSource.BranchArtifact, ExecutedSpecIds: [HeadId, TailId], Series: Series);
    }

    private sealed class CapturingPublisher : ISpecSetPublisher
    {
        public SpecSet? Set { get; private set; }

        public Task<CommandResult> PublishAsync(
            PipelineContext pipeline, string project, SeriesScope scope, SpecSet set,
            IReadOnlyList<IgnoredInstruction> ignoredInstructions, CancellationToken ct)
        {
            Set = set;
            return Task.FromResult(CommandResult.Ok("published"));
        }

        public bool Kept { get; private set; }

        public Task<CommandResult> KeepAsync(
            PipelineContext pipeline, SeriesScope scope, SpecSet set, string revisionSha, CancellationToken ct)
        {
            (Set, Kept) = (set, true);
            return Task.FromResult(CommandResult.Ok("kept"));
        }
    }

    private sealed class SeededFileReader : ISandboxFileReader
    {
        private readonly Dictionary<string, string> _files = new(StringComparer.Ordinal);

        public void Seed(string path, string content) => _files[path] = content;

        public Task<bool> ExistsAsync(string path, CancellationToken ct) => Task.FromResult(_files.ContainsKey(path));
        public Task<string?> TryReadAsync(string path, CancellationToken ct) =>
            Task.FromResult(_files.TryGetValue(path, out var c) ? c : null);
        public Task<string> ReadRequiredAsync(string path, CancellationToken ct) => Task.FromResult(_files[path]);
        public Task<IReadOnlyList<string>> ListAsync(string path, int? maxDepth, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<string>>([.. _files.Keys.Where(k => k.StartsWith(path, StringComparison.Ordinal))]);
        public Task WriteAsync(string path, string content, CancellationToken ct)
        {
            _files[path] = content;
            return Task.CompletedTask;
        }
    }
}
