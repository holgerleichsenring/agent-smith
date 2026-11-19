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
/// 2026-09-08-4aa9, run a109's other half through the real reader: the branch carries a
/// set whose first spec executed and the ticket text is unchanged. An operator comment
/// after our derivation-time comment keeps the tail and hands the comment to its coding
/// master (2026-11-19-6b0ba); no comment continues the tail unchanged — and, the pointer
/// at the marker's own commit, the cause is a re-trigger, not a reviewer edit.
/// </summary>
public sealed class DeriveSpecCommentRecutTests
{
    private const string Key = "azdo-19106";
    // 2026-10-06-03c7c: the series the branch's set carries, and the members its specs are.
    private const string Series = "2026-10-06-0a0a";
    private static readonly string HeadId = SeriesIdFactory.Member(Series, 0);
    private static readonly string TailId = SeriesIdFactory.Member(Series, 1);
    private const string DerivationSha = "spec-sha-1";
    private const string MarkerSha = "marker-sha-2";
    private const string TicketText = """
        Migrate the client.

        Ping me if unclear.
        """;

    [Fact]
    public async Task DeriveSpec_ACommentAfterAnExecutedSpec_KeepsTheTailAndHandsTheCommentToItsMaster()
    {
        var published = new CapturingPublisher();
        var thread = new[] { OurCut(), Operator("spec b is wrong: the callers stay where they are") };

        // a109's live shape: the pointer still names the derivation commit, not the marker's.
        var context = Context(thread);
        var result = await Handler(published, pointerSha: DerivationSha).ExecuteAsync(context, default);

        result.IsSuccess.Should().BeTrue();
        context.Pipeline.Get<string>(ContextKeys.SpecKeptInput).Should().Be(SpecRevisionCause.Comment,
            "the unbuilt spec is kept and its coding master amends it for the comment (2026-11-19-6b0ba)");
        published.Set!.StandIns.Should().BeEmpty();
        published.Set!.Specs.Select(p => p.SpecId).Should().Equal(HeadId, TailId);
        published.Set.Executed.Should().Equal([HeadId], "the executed spec is carried, never edited");
        published.Set.Current.Cause.Should().Be(SpecRevisionCause.Comment);
    }

    [Fact]
    public async Task DeriveSpec_NoCommentAfterAnExecutedSpec_ContinuesTheTailUnchanged()
    {
        var published = new CapturingPublisher();

        await Handler(published, pointerSha: MarkerSha).ExecuteAsync(Context([OurCut()]), default);

        published.Kept.Should().BeTrue(
            "the marker's commit is this system's own — the pointer names it, so nothing arrived (2026-11-17-7c9ba)");
        published.Set!.Current.Cause.Should().Be(SpecRevisionCause.Initial, "no revision is appended to a kept set");
        published.Set.Specs.Select(p => p.Draft.Goal).Should().Equal($"Goal {HeadId}", $"Goal {TailId}");
    }

    /// <summary>
    /// 2026-10-06-03c7c: a run over new input reads the series' base off the branch — it is never
    /// minted a second time, so the executed head keeps its ids.
    /// </summary>
    [Fact]
    public async Task DeriveSpec_Rederivation_ReusesSeriesFromBranch()
    {
        var published = new CapturingPublisher();
        var thread = new[] { OurCut(), Operator("spec b is wrong: the callers stay where they are") };

        await Handler(published, pointerSha: DerivationSha).ExecuteAsync(Context(thread), default);

        published.Set!.Series.Should().Be(Series);
    }

    /// <summary>
    /// 2026-10-06-03c7c: a run with no database — the CLI's in-memory pointer store, empty on every
    /// run — still finds the same base, because the branch is where it is read first.
    /// </summary>
    [Fact]
    public async Task DeriveSpec_NoDatabase_ReusesSeriesFromBranch()
    {
        var published = new CapturingPublisher();
        var thread = new[] { OurCut(), Operator("spec b is wrong: the callers stay where they are") };

        await Handler(published, pointerSha: null).ExecuteAsync(Context(thread), default);

        published.Set!.Series.Should().Be(Series, "no pointer row exists, and the branch still names the base");
        published.Set.Specs.Should().OnlyContain(p => p.SpecId.StartsWith(Series, StringComparison.Ordinal));
    }

    private static DeriveSpecHandler Handler(
        ISpecSetPublisher publisher, string? pointerSha)
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

    private static DeriveSpecContext Context(IReadOnlyList<TicketComment> thread)
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
        return new DeriveSpecContext(
            Ticket(), null, [new RepoConnection { Name = "primary" }], new AgentConfig(), pipeline);
    }

    private static Ticket Ticket() =>
        new(new TicketId("19106"), "Migrate the client", TicketText, null, "open", "azdo", []);

    private static TicketComment OurCut() => new(
        "agent-smith", DateTimeOffset.UtcNow.AddHours(-2),
        SpecSetComment.Render(BranchSet(), null, AgentSmith.Tests.TestSupport.SeriesSettingsDoubles.Defaults));

    private static TicketComment Operator(string body) =>
        new("operator", DateTimeOffset.UtcNow.AddHours(-1), body);

    private static SeededFileReader SeededFiles()
    {
        var files = new SeededFileReader();
        files.Seed($".agentsmith/series/{Series}.yaml", SetYaml(TicketTextFingerprint.Of(Ticket())));
        files.Seed($".agentsmith/specs/done/{HeadId}-first.yaml", SpecYaml(HeadId));
        files.Seed($".agentsmith/specs/planned/{TailId}-second.yaml", SpecYaml(TailId));
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

    // The set the branch carries, as our cut comment rendered it.
    private static SpecSet BranchSet()
    {
        var segments = TicketSegmenter.Segment(TicketText);
        var carries = segments.Select(s => s.Id).ToList();
        DerivedSpec Spec(string id, string goal) => new(
            new SpecDraft(id, goal, $"spec: {id}\ngoal: \"{goal}\"", []) { Done = [$"Done {id}."] },
            id, string.Empty, carries);
        var specs = new[] { Spec(HeadId, $"Goal {HeadId}"), Spec(TailId, $"Goal {TailId}") };
        return new SpecSet(
            Key, specs, SpecAccountingBuilder.Build(specs, [], segments),
            [new SpecRevision(1, SpecRevisionCause.Initial, DateTimeOffset.UtcNow)],
            SpecSource.BranchArtifact, ExecutedSpecIds: [HeadId], Series: Series);
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
