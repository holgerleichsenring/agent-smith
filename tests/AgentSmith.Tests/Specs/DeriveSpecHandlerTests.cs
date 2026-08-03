using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Events;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.PhaseExecution;
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
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// p0393a: the fail-safe. If the accounting cannot be produced the run does NOT split at
/// all — one phase with the whole ticket pinned, and a run event naming why. Fail safe
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
    public async Task Accounting_Incomplete_EmitsOnePhaseWithTheFullTicketPinnedAndARunEvent()
    {
        var events = new RecordingEventPublisher();
        var published = new CapturingPublisher();
        // A cut whose accounting leaves segments unspoken for.
        var incomplete = Derivation(carries: [1], discarded: []);

        var result = await Handler(events, published, incomplete)
            .ExecuteAsync(Context(), default);

        result.IsSuccess.Should().BeTrue();
        var set = published.Set!;
        set.Phases.Should().ContainSingle("an unaccounted split does not split at all");
        set.TicketPinnedWhole.Should().BeTrue();
        set.Phases[0].Markdown.Should().Contain("`<Domain>ServiceClient`",
            "the whole ticket is carried, verbatim, by the one phase");
        events.Gates.Should().ContainSingle(g => g.Gate == "spec-accounting" && !g.Passed,
            "an unaccounted ticket that quietly becomes one phase reads like a ticket that had one");
    }

    [Fact]
    public async Task Accounting_Incomplete_CarriesTheRejectedCutsDoneCriteriaForward()
    {
        var published = new CapturingPublisher();
        var incomplete = Derivation(carries: [1], discarded: []);

        await Handler(new RecordingEventPublisher(), published, incomplete)
            .ExecuteAsync(Context(), default);

        published.Set!.Phases[0].Draft.Done.Should().Contain("Every call site is renamed.",
            "the cut was refused for its COVERAGE, not for its criteria — the acceptance "
            + "contract must not silently empty");
    }

    [Fact]
    public async Task DeriveSpec_ModelProducedNothingUsable_StillProducesOnePhaseAndSaysWhy()
    {
        var events = new RecordingEventPublisher();
        var published = new CapturingPublisher();

        var result = await Handler(events, published, derivation: null)
            .ExecuteAsync(Context(), default);

        result.IsSuccess.Should().BeTrue();
        published.Set!.Phases.Should().ContainSingle();
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

    private static DeriveSpecHandler Handler(
        RecordingEventPublisher events, CapturingPublisher publisher, SpecDerivation? derivation)
    {
        var deriver = new Mock<ISpecSetDeriver>();
        deriver.Setup(d => d.DeriveAsync(
                It.IsAny<Ticket>(), It.IsAny<IReadOnlyList<TicketSegment>>(), It.IsAny<SpecSet?>(),
                It.IsAny<string>(), It.IsAny<AgentConfig>(), It.IsAny<PipelineContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((derivation, derivation is null ? "no parseable cut" : null));

        var validator = new SpecDraftValidator(new PhaseSpecSchemaProvider());
        var draftReader = new PhaseDraftReader();
        return new DeriveSpecHandler(
            deriver.Object,
            Mock.Of<ISpecSetReader>(),
            publisher,
            new InMemorySpecSetPointerStore(),
            new SpecSourceResolver(
                new PhaseSpecFromTicket(validator, draftReader),
                NullLogger<SpecSourceResolver>.Instance),
            new SpecFallback(validator, draftReader),
            new SpecSetTicketCommenter(
                Mock.Of<ITicketProviderFactory>(), NullLogger<SpecSetTicketCommenter>.Instance),
            events,
            NullLogger<DeriveSpecHandler>.Instance);
    }

    private static DeriveSpecContext Context()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.RunId, "run-1");
        return new DeriveSpecContext(
            new Ticket(new TicketId("19106"), "Migrate the client", Ticket, null, "open", "azdo", []),
            null,
            [new RepoConnection { Name = "primary" }],
            new AgentConfig(),
            pipeline);
    }

    // The judgement half, already parsed — the handler's job is what happens to it.
    private static SpecDerivation Derivation(
        IReadOnlyList<int> carries, IReadOnlyList<DiscardedSegment> discarded)
    {
        var segments = TicketSegmenter.Segment(Ticket);
        var phase = new SpecPhase(
            new Contracts.Models.PhaseDraft("p19106a", "Rename the call sites", "phase: p19106a", [])
            {
                Done = ["Every call site is renamed."],
            },
            "rename-the-call-sites",
            SegmentExtractor.BuildMarkdown("p19106a", "Rename", carries, segments),
            carries);
        return new SpecDerivation(
            new SpecSet(
                "azdo-19106", [phase],
                SpecAccountingBuilder.Build([phase], discarded, segments),
                [new SpecRevision(1, SpecRevisionCause.Initial, DateTimeOffset.UtcNow)],
                SpecSource.Derived),
            []);
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
