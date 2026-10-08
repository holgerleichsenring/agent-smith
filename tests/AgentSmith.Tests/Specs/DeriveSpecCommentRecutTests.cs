using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Events;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.PhaseExecution;
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
/// set whose first phase executed and the ticket text is unchanged. An operator comment
/// after our derivation-time comment calls the deriver with the branch set and the
/// comment cause; no comment continues the tail with no deriver call — and, the pointer
/// at the marker's own commit, the cause is a re-trigger, not a reviewer edit.
/// </summary>
public sealed class DeriveSpecCommentRecutTests
{
    private const string Key = "azdo-19106";
    // 2026-10-06-03c7c: the series the branch's set carries, and the members its phases are.
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
    public async Task DeriveSpec_ACommentAfterAnExecutedPhase_CallsTheDeriverWithTheSetAndTheCause()
    {
        var deriver = new CapturingDeriver(RecutTail());
        var published = new CapturingPublisher();
        var thread = new[] { OurCut(), Operator("phase b is wrong: the callers stay where they are") };

        // a109's live shape: the pointer still names the derivation commit, not the marker's.
        var result = await Handler(deriver, published, pointerSha: DerivationSha).ExecuteAsync(Context(thread), default);

        result.IsSuccess.Should().BeTrue();
        deriver.Calls.Should().Be(1, "the comment is new input the model has not seen");
        deriver.CauseSeen.Should().Be(SpecRevisionCause.Comment);
        deriver.PreviousSeen!.Executed.Should().Equal(HeadId);
        published.Set!.Phases.Select(p => p.PhaseId).Should().Equal(HeadId, TailId);
        published.Set.Executed.Should().Equal([HeadId], "the executed phase is carried, never edited");
        published.Set.Current.Cause.Should().Be(SpecRevisionCause.Comment);
    }

    [Fact]
    public async Task DeriveSpec_NoCommentAfterAnExecutedPhase_ContinuesTheTailWithoutTheDeriver()
    {
        var deriver = new CapturingDeriver(RecutTail());
        var published = new CapturingPublisher();

        await Handler(deriver, published, pointerSha: MarkerSha).ExecuteAsync(Context([OurCut()]), default);

        deriver.Calls.Should().Be(0, "nothing new: the ticket is unchanged and nobody commented");
        published.Set!.Current.Cause.Should().Be(SpecRevisionCause.Retrigger,
            "the marker's commit is this system's own — the pointer names it");
        published.Set.Phases.Select(p => p.Draft.Goal).Should().Equal($"Goal {HeadId}", $"Goal {TailId}");
    }

    /// <summary>
    /// 2026-10-06-03c7c: a re-derivation reads the series' base off the branch and hands it to the
    /// deriver — it is never minted a second time, so the executed head keeps its ids.
    /// </summary>
    [Fact]
    public async Task DeriveSpec_Rederivation_ReusesSeriesFromBranch()
    {
        var deriver = new CapturingDeriver(RecutTail());
        var published = new CapturingPublisher();
        var thread = new[] { OurCut(), Operator("phase b is wrong: the callers stay where they are") };

        await Handler(deriver, published, pointerSha: DerivationSha).ExecuteAsync(Context(thread), default);

        deriver.SeriesSeen.Should().Be(Series, "the base is read off the set on the branch");
        published.Set!.Series.Should().Be(Series);
    }

    /// <summary>
    /// 2026-10-06-03c7c: a run with no database — the CLI's in-memory pointer store, empty on every
    /// run — still finds the same base, because the branch is where it is read first.
    /// </summary>
    [Fact]
    public async Task DeriveSpec_NoDatabase_ReusesSeriesFromBranch()
    {
        var deriver = new CapturingDeriver(RecutTail());
        var published = new CapturingPublisher();
        var thread = new[] { OurCut(), Operator("phase b is wrong: the callers stay where they are") };

        await Handler(deriver, published, pointerSha: null).ExecuteAsync(Context(thread), default);

        published.Set!.Series.Should().Be(Series, "no pointer row exists, and the branch still names the base");
        published.Set.Phases.Should().OnlyContain(p => p.PhaseId.StartsWith(Series, StringComparison.Ordinal));
    }

    private static DeriveSpecHandler Handler(
        ISpecSetDeriver deriver, ISpecSetPublisher publisher, string? pointerSha)
    {
        var files = SeededFiles();
        var factory = new Mock<ISandboxFileReaderFactory>();
        factory.Setup(f => f.Create(It.IsAny<ISandbox>())).Returns(files);
        var gitOps = new SandboxGitOperations(
            new GitBranchPusher(), AgentSmith.Tests.TestSupport.TestGitCredentials.Resolver, NullLogger<SandboxGitOperations>.Instance, factory.Object,
            new SandboxGitIdentity(NullLogger<SandboxGitIdentity>.Instance));
        var draftReader = new PhaseDraftReader();
        var reader = AgentSmith.Tests.TestSupport.SeriesDoubles.Reader(factory.Object, gitOps);
        // The last commit on the spec path is the marker's; the pointer names what the caller says.
        var pointers = new InMemorySpecSetPointerStore();
        if (pointerSha is not null)
            pointers.SaveAsync(string.Empty, new SpecSetPointer(Key, "primary", pointerSha, 1), default)
                .GetAwaiter().GetResult();
        var validator = new SpecDraftValidator(new PhaseSpecSchemaProvider());
        var tickets = new Mock<ITicketProviderFactory>();
        return new DeriveSpecHandler(
            deriver, reader, publisher, pointers,
            new ApprovedSpecSetResolver(
                new InMemorySpecApprovalStore(), NullLogger<ApprovedSpecSetResolver>.Instance),
            new SpecSourceResolver(
                new ApprovedSetHandoff(NullLogger<ApprovedSetHandoff>.Instance),
                new FiledTicketSpecGate(NullLogger<FiledTicketSpecGate>.Instance),
                NullLogger<SpecSourceResolver>.Instance),
            new SpecFallback(validator, draftReader, new DerivedPhaseYamlRenderer()),
            new SpecCoverageRefusal(
                new SpecCutGate(new NoOpEventPublisher(), NullLogger<SpecCutGate>.Instance),
                new SpecFallback(validator, draftReader, new DerivedPhaseYamlRenderer())),
            new SpecSetTicketCommenter(tickets.Object, Moq.Mock.Of<AgentSmith.Contracts.Reviews.IFullSetPrNotice>(), NullLogger<SpecSetTicketCommenter>.Instance),
            ApprovedSetDoubles.KeptNotice(),
            new SpecCutGate(new NoOpEventPublisher(), NullLogger<SpecCutGate>.Instance),
            new UnansweredQuestionPin(NullLogger<UnansweredQuestionPin>.Instance),
            new UnansweredQuestionNotice(tickets.Object, NullLogger<UnansweredQuestionNotice>.Instance),
            new SeriesResolver(new SeriesIdFactory(TimeProvider.System)),
            NullLogger<DeriveSpecHandler>.Instance);
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
        SpecSetComment.Render(RecutTail().Set, null));

    private static TicketComment Operator(string body) =>
        new("operator", DateTimeOffset.UtcNow.AddHours(-1), body);

    private static SeededFileReader SeededFiles()
    {
        var files = new SeededFileReader();
        files.Seed($".agentsmith/series/{Series}.yaml", SetYaml(TicketTextFingerprint.Of(Ticket())));
        files.Seed($".agentsmith/specs/done/{HeadId}-first.yaml", PhaseYaml(HeadId));
        files.Seed($".agentsmith/specs/planned/{TailId}-second.yaml", PhaseYaml(TailId));
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

    private static string PhaseYaml(string id) => $"""
        spec: {id}
        goal: "Goal {id}"
        done:
          - "Done {id}."
        """;

    // The model's reply: the executed head repeated, the tail cut again with the comment in view.
    private static SpecDerivation RecutTail()
    {
        var segments = TicketSegmenter.Segment(TicketText);
        var carries = segments.Select(s => s.Id).ToList();
        SpecPhase Phase(string id, string goal) => new(
            new PhaseDraft(id, goal, $"spec: {id}\ngoal: \"{goal}\"", []) { Done = [$"Done {id}."] },
            id, string.Empty, carries);
        var phases = new[] { Phase(HeadId, $"Goal {HeadId}"), Phase(TailId, "Cut again with the comment in view") };
        return new SpecDerivation(
            new SpecSet(
                Key, phases, SpecAccountingBuilder.Build(phases, [], segments),
                [new SpecRevision(1, SpecRevisionCause.Initial, DateTimeOffset.UtcNow)],
                SpecSource.BranchArtifact, ExecutedPhaseIds: [HeadId], Series: Series),
            []);
    }

    private sealed class CapturingDeriver(SpecDerivation derivation) : ISpecSetDeriver
    {
        public int Calls { get; private set; }
        public SpecSet? PreviousSeen { get; private set; }
        public string? CauseSeen { get; private set; }
        public string? SeriesSeen { get; private set; }

        public Task<(SpecDerivation? Derivation, string? Error)> DeriveAsync(
            Ticket ticket, IReadOnlyList<TicketSegment> segments, SpecSet? previous, string series,
            string cause, AgentConfig agentConfig, PipelineContext pipeline, CancellationToken cancellationToken)
        {
            Calls++;
            PreviousSeen = previous;
            CauseSeen = cause;
            SeriesSeen = series;
            return Task.FromResult<(SpecDerivation?, string?)>((derivation, null));
        }
    }

    private sealed class CapturingPublisher : ISpecSetPublisher
    {
        public SpecSet? Set { get; private set; }

        public Task<CommandResult> PublishAsync(
            PipelineContext pipeline, string project, RepoConnection carryingRepo, SpecSet set,
            IReadOnlyList<IgnoredInstruction> ignoredInstructions, CancellationToken ct)
        {
            Set = set;
            return Task.FromResult(CommandResult.Ok("published"));
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
