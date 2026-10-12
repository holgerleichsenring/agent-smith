using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Events;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.SpecExecution;
using AgentSmith.Application.Services.Persistence;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Validation;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// p0393a: the fail-safe. If the accounting cannot be produced the run does NOT split at
/// all — one spec with the whole ticket pinned, and a run event naming why. Fail safe
/// means falling back to a shape that is known to work, not partially applying one that
/// is not: degrading to too much context is cheap, degrading to a missing manual nobody
/// noticed is the failure this whole line exists to prevent.
/// </summary>
public sealed class DeriveSpecHandlerTests
{
    private const string Ticket = """
        Migrate the client.

        Call sites MUST be named `<Domain>ServiceClient`.

        Ping me if unclear.
        """;

    [Fact]
    public async Task Accounting_Incomplete_EmitsOneSpecWithTheFullTicketPinnedAndARunEvent()
    {
        var events = new RecordingEventPublisher();
        var published = new CapturingPublisher();
        // A cut whose accounting leaves segments unspoken for.
        var incomplete = Derivation(carries: [1], discarded: []);

        var result = await Handler(events, published, incomplete)
            .ExecuteAsync(Context(), default);

        result.IsSuccess.Should().BeTrue();
        var set = published.Set!;
        set.Specs.Should().ContainSingle("an unaccounted split does not split at all");
        set.TicketPinnedWhole.Should().BeTrue();
        set.Specs[0].Markdown.Should().Contain("`<Domain>ServiceClient`",
            "the whole ticket is carried, verbatim, by the one spec");
        events.Gates.Should().ContainSingle(g => g.Gate == "spec-accounting" && !g.Passed,
            "an unaccounted ticket that quietly becomes one spec reads like a ticket that had one");
    }

    [Fact]
    public async Task Accounting_Incomplete_CarriesTheRejectedCutsDoneCriteriaForward()
    {
        var published = new CapturingPublisher();
        var incomplete = Derivation(carries: [1], discarded: []);

        await Handler(new RecordingEventPublisher(), published, incomplete)
            .ExecuteAsync(Context(), default);

        published.Set!.Specs[0].Draft.Done.Should().Contain("Every call site is renamed.",
            "the cut was refused for its COVERAGE, not for its criteria — the acceptance "
            + "contract must not silently empty");
    }

    [Fact]
    public async Task DeriveSpec_ModelProducedNothingUsable_StillProducesOneSpecAndSaysWhy()
    {
        var events = new RecordingEventPublisher();
        var published = new CapturingPublisher();

        var result = await Handler(events, published, derivation: null)
            .ExecuteAsync(Context(), default);

        result.IsSuccess.Should().BeTrue();
        published.Set!.Specs.Should().ContainSingle();
        published.Set.TicketPinnedWhole.Should().BeTrue();
        events.Gates.Should().ContainSingle(g => g.Gate == "spec-accounting");
    }

    [Fact]
    public async Task Accounting_Complete_KeepsTheDerivedSplit()
    {
        var published = new CapturingPublisher();
        var complete = Derivation(carries: [1, 2], discarded: [new DiscardedSegment(3, "sign-off")]);

        await Handler(new RecordingEventPublisher(), published, complete)
            .ExecuteAsync(Context(), default);

        published.Set!.TicketPinnedWhole.Should().BeFalse();
        published.Set.Accounting.IsComplete.Should().BeTrue();
    }

    // 2026-10-12-f5f7a: over several repositories the whole-ticket fallback would put the whole
    // ticket into one repository's spec — a guess. The ticket is handed back naming them instead.
    [Fact]
    public async Task DeriveSpecHandler_MultiRepoFallback_HandsBack()
    {
        var events = new RecordingEventPublisher();
        var published = new CapturingPublisher();

        await Handler(events, published, derivation: null).ExecuteAsync(Context("BackgroundWorker", "Server"), default);

        published.Set!.Specs.Should().BeEmpty();
        published.Set.Handback!.Case.Should().Be(SpecHandbackCase.ScopeUnassigned);
        published.Set.Handback.Reason.Should().Contain("BackgroundWorker, Server");
        events.Gates.Should().ContainSingle(g => g.Gate == "spec-accounting" && !g.Passed,
            "the failed derivation is still reported");
    }

    [Fact]
    public async Task SpecCoverageRefusal_MultiRepo_HandsBack()
    {
        var events = new RecordingEventPublisher();
        var published = new CapturingPublisher();
        var incomplete = Derivation(carries: [1], discarded: []);

        await Handler(events, published, incomplete).ExecuteAsync(Context("BackgroundWorker", "Server"), default);

        published.Set!.IsHandedBack.Should().BeTrue();
        published.Set.TicketPinnedWhole.Should().BeFalse();
        published.Set.Handback!.Reason.Should().Contain("BackgroundWorker, Server");
        events.Gates.Should().ContainSingle(g => g.Gate == "spec-accounting" && !g.Passed);
    }

    // 2026-10-12-f5f7f: a series whose repositories disagree is not derived from or written over —
    // it is handed back as its own case naming them, before any model call, and nothing is written.
    [Fact]
    public async Task DeriveSpecHandler_LaggingRead_HandsBackOutOfStep_NoModelNoWrite()
    {
        var published = new CapturingPublisher();
        var deriver = new Mock<ISpecSetDeriver>(MockBehavior.Strict);
        var pointers = new InMemorySpecSetPointerStore();
        var read = new SpecSetReadResult(Derivation(carries: [1, 2], discarded: []).Set, "sha-Server")
        {
            Lagging = ["Client"],
        };
        var reader = new Mock<ISpecSetReader>();
        reader.Setup(r => r.ReadAsync(
                It.IsAny<PipelineContext>(), It.IsAny<RepoConnection>(), It.IsAny<IReadOnlyList<RepoConnection>>(),
                It.IsAny<TicketKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SpecSetOnBranch.Answered(read));

        var context = Context("Server", "Client");
        var result = await Handler(new RecordingEventPublisher(), published, null, reader.Object, deriver, pointers)
            .ExecuteAsync(context, default);

        result.IsSuccess.Should().BeTrue("the run parks; it does not fail");
        result.Message.Should().StartWith("awaiting_user_input").And.Contain("Client").And.Contain("out of step");
        context.Pipeline.Get<SpecHandback>(ContextKeys.SpecHandback).Case.Should().Be(SpecHandbackCase.SeriesOutOfStep);
        published.Set.Should().BeNull("nothing is written");
        deriver.VerifyNoOtherCalls();
        (await pointers.GetAsync(string.Empty, "azdo-19106", default)).Should().BeNull("the pointer is untouched");
    }

    // 2026-09-22-6ad7: the reader answers "nothing at the path", never null — a default-valued
    // mock would hand the handler a null branch answer and the split would read as a crash.
    private static ISpecSetReader EmptyBranch()
    {
        var reader = new Mock<ISpecSetReader>();
        reader.Setup(r => r.ReadAsync(
                It.IsAny<PipelineContext>(), It.IsAny<RepoConnection>(), It.IsAny<IReadOnlyList<RepoConnection>>(),
                It.IsAny<TicketKey>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(SpecSetOnBranch.Nothing);
        return reader.Object;
    }

    private static DeriveSpecHandler Handler(
        RecordingEventPublisher events, CapturingPublisher publisher, SpecDerivation? derivation,
        ISpecSetReader? reader = null, Mock<ISpecSetDeriver>? strictDeriver = null,
        ISpecSetPointerStore? pointers = null)
    {
        var deriver = strictDeriver ?? new Mock<ISpecSetDeriver>();
        if (strictDeriver is null) deriver.Setup(d => d.DeriveAsync(
                It.IsAny<Ticket>(), It.IsAny<IReadOnlyList<TicketSegment>>(), It.IsAny<SpecSet?>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<AgentConfig>(),
                It.IsAny<PipelineContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((derivation, derivation is null ? "no parseable cut" : null));

        var validator = new SpecDraftValidator(new SpecSchemaProvider());
        var draftReader = new SpecDraftReader();
        return new DeriveSpecHandler(
            deriver.Object,
            SeriesDoubles.BranchRead(reader ?? EmptyBranch()),
            publisher,
            pointers ?? new InMemorySpecSetPointerStore(),
            new ApprovedSpecSetResolver(
                new InMemorySpecApprovalStore(), NullLogger<ApprovedSpecSetResolver>.Instance),
            new SpecSourceResolver(
                new ApprovedSetHandoff(NullLogger<ApprovedSetHandoff>.Instance),
                new FiledTicketSpecGate(NullLogger<FiledTicketSpecGate>.Instance),
                NullLogger<SpecSourceResolver>.Instance),
            new SpecFallback(validator, draftReader, new DerivedSpecYamlRenderer()),
            new SpecCoverageRefusal(
                new SpecCutGate(events, NullLogger<SpecCutGate>.Instance),
                new SpecFallback(validator, draftReader, new DerivedSpecYamlRenderer())),
            new SpecSetTicketCommenter(
                Mock.Of<ITicketProviderFactory>(), Moq.Mock.Of<AgentSmith.Contracts.Reviews.IFullSetPrNotice>(), NullLogger<SpecSetTicketCommenter>.Instance),
            ApprovedSetDoubles.KeptNotice(),
            new SpecCutGate(events, NullLogger<SpecCutGate>.Instance),
            new UnansweredQuestionPin(NullLogger<UnansweredQuestionPin>.Instance),
            new UnansweredQuestionNotice(
                Mock.Of<ITicketProviderFactory>(), NullLogger<UnansweredQuestionNotice>.Instance),
            new SeriesResolver(new SeriesIdFactory(TimeProvider.System)),
            AgentSmith.Tests.TestSupport.HandbackTestDoubles.Probe(),
            NullLogger<DeriveSpecHandler>.Instance);
    }

    private static DeriveSpecContext Context(params string[] repos)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.RunId, "run-1");
        return new DeriveSpecContext(
            new Ticket(new TicketId("19106"), "Migrate the client", Ticket, null, "open", "azdo", []),
            null,
            repos.Length == 0
                ? [new RepoConnection { Name = "primary" }]
                : [.. repos.Select(name => new RepoConnection { Name = name })],
            new AgentConfig(),
            pipeline);
    }

    // The judgement half, already parsed — the handler's job is what happens to it.
    private static SpecDerivation Derivation(
        IReadOnlyList<int> carries, IReadOnlyList<DiscardedSegment> discarded)
    {
        var segments = TicketSegmenter.Segment(Ticket);
        var spec = new DerivedSpec(
            new Contracts.Models.SpecDraft("p19106a", "Rename the call sites", "spec: p19106a", [])
            {
                Done = ["Every call site is renamed."],
            },
            "rename-the-call-sites",
            SegmentExtractor.BuildMarkdown("p19106a", "Rename", carries, segments),
            carries);
        return new SpecDerivation(
            new SpecSet(
                "azdo-19106", [spec],
                SpecAccountingBuilder.Build([spec], discarded, segments),
                [new SpecRevision(1, SpecRevisionCause.Initial, DateTimeOffset.UtcNow)],
                SpecSource.Derived),
            []);
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
    }

    private sealed class RecordingEventPublisher : Contracts.Events.IEventPublisher
    {
        public List<Contracts.Events.GateCheckedEvent> Gates { get; } = [];

        public Task PublishAsync(Contracts.Events.RunEvent runEvent, CancellationToken ct = default)
        {
            if (runEvent is Contracts.Events.GateCheckedEvent gate) Gates.Add(gate);
            return Task.CompletedTask;
        }
    }
}
