using AgentSmith.Application.Models;
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
/// 2026-09-07-c9d4, the second run through DeriveSpec: the branch carries a question
/// hand-back, the ticket was re-triggered. Nobody answered → the taken reading is pinned
/// into the derivation as the answer, the cut proceeds, and the ticket is told on which
/// reading. Somebody answered → no pin; the derivation reads the answer in the thread,
/// and parks again only if the model asks again.
/// </summary>
public sealed class DeriveSpecQuestionTests
{
    private const string ReadingA = "a major only where nothing lower clears the advisory";
    private const string ReadingB = "the newest major everywhere, breaking changes included";
    private const string Ticket = """
        Adopt the newest versions, even breaking.

        Ping me if unclear.
        """;

    private static readonly DateTimeOffset Asked = new(2026, 9, 7, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Question_ASecondUnansweredPark_ProceedsAndSaysOnWhichReading()
    {
        var tickets = new Mock<ITicketProvider>();
        var deriver = new CapturingDeriver(PhasesCut());
        var published = new CapturingPublisher();
        var context = Context(OurQuestion(Asked));

        var result = await Handler(deriver, published, tickets).ExecuteAsync(context, default);

        result.IsSuccess.Should().BeTrue();
        deriver.PinSeen.Should().Contain("Reading (a) is", "the pin is in place BEFORE the model runs");
        deriver.PinSeen.Should().Contain("(a) " + ReadingA).And.Contain("(b) " + ReadingB);
        published.Set!.IsHandedBack.Should().BeFalse();
        published.Set.Phases.Should().ContainSingle("the run proceeds with real phases");
        tickets.Verify(t => t.UpdateStatusAsync(
            It.IsAny<TicketId>(), It.Is<string>(c => c.Contains("proceeding on reading (a)")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // The second run's reply is the model's SECOND phrasing; the notice names the reading
    // from the persisted first question, never from what the model says now.
    [Fact]
    public async Task Question_AProceedNotice_NamesTheTakenReadingFromTheFirstQuestion()
    {
        var tickets = new Mock<ITicketProvider>();
        var deriver = new CapturingDeriver(PhasesCut(goal: "Go conservative: bump only what an advisory forces"));
        var context = Context(OurQuestion(Asked));

        await Handler(deriver, new CapturingPublisher(), tickets).ExecuteAsync(context, default);

        tickets.Verify(t => t.UpdateStatusAsync(
            It.IsAny<TicketId>(),
            It.Is<string>(c => c.Contains("> (a) " + ReadingA) && !c.Contains("Go conservative")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Question_AnAnsweredQuestion_ParksAgainOnlyIfTheModelAsksAgain()
    {
        var tickets = new Mock<ITicketProvider>();
        var answered = Context(
            OurQuestion(Asked),
            new TicketComment("operator", Asked.AddHours(1), "(b) — the newest major everywhere, please"));

        // The model reads the answer in the thread and cuts: no pin, no notice, real phases.
        var cutting = new CapturingDeriver(PhasesCut());
        var cut = new CapturingPublisher();
        await Handler(cutting, cut, tickets).ExecuteAsync(answered, default);
        cutting.PinSeen.Should().BeNull("an answered question is derived on the answer, not on the pin");
        cut.Set!.IsHandedBack.Should().BeFalse();

        // The model asks AGAIN despite the answer: it parks again — the notice is not posted.
        var asking = new CapturingDeriver(QuestionAgain());
        var parked = new CapturingPublisher();
        await Handler(asking, parked, tickets).ExecuteAsync(Context(
            OurQuestion(Asked),
            new TicketComment("operator", Asked.AddHours(1), "(b) — the newest major everywhere, please")), default);
        parked.Set!.IsHandedBack.Should().BeTrue();
        parked.Set.Handback!.Case.Should().Be(SpecHandbackCase.Question);
        tickets.Verify(t => t.UpdateStatusAsync(
            It.IsAny<TicketId>(), It.Is<string>(c => c.Contains("proceeding on reading")),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Question_ModelAsksAgainDespiteThePin_ParksAgainWithoutANotice()
    {
        var tickets = new Mock<ITicketProvider>();
        var asking = new CapturingDeriver(QuestionAgain());
        var parked = new CapturingPublisher();

        await Handler(asking, parked, tickets).ExecuteAsync(Context(OurQuestion(Asked)), default);

        asking.PinSeen.Should().NotBeNull();
        parked.Set!.IsHandedBack.Should().BeTrue("the model's second question parks — the pin makes that the exception, not the rule");
        tickets.Verify(t => t.UpdateStatusAsync(
            It.IsAny<TicketId>(), It.Is<string>(c => c.Contains("proceeding on reading")),
            It.IsAny<CancellationToken>()), Times.Never, "a run that parked did not proceed on anything");
    }

    private static DeriveSpecHandler Handler(
        ISpecSetDeriver deriver, ISpecSetPublisher publisher, Mock<ITicketProvider> tickets)
    {
        var factory = new Mock<ITicketProviderFactory>();
        factory.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(tickets.Object);
        var validator = new SpecDraftValidator(new PhaseSpecSchemaProvider());
        var draftReader = new PhaseDraftReader();
        var reader = new Mock<ISpecSetReader>();
        reader.Setup(r => r.ReadAsync(
                It.IsAny<PipelineContext>(), It.IsAny<RepoConnection>(), It.IsAny<SpecSetKey>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SpecSetReadResult(PreviousQuestion(), "sha-1"));
        var pointers = new InMemorySpecSetPointerStore();
        pointers.SaveAsync(string.Empty,
            new SpecSetPointer("azdo-19106", "primary", "sha-1", 1, SpecHandbackCase.Question, 1, "sha-1"),
            CancellationToken.None).GetAwaiter().GetResult();
        return new DeriveSpecHandler(
            deriver, reader.Object, publisher, pointers,
            new SpecSourceResolver(new PhaseSpecFromTicket(validator, draftReader), NullLogger<SpecSourceResolver>.Instance),
            new SpecFallback(validator, draftReader, new DerivedPhaseYamlRenderer()),
            new SpecSetTicketCommenter(factory.Object, NullLogger<SpecSetTicketCommenter>.Instance),
            new SpecCutGate(new Application.Services.Events.NoOpEventPublisher(), NullLogger<SpecCutGate>.Instance),
            new UnansweredQuestionPin(NullLogger<UnansweredQuestionPin>.Instance),
            new UnansweredQuestionNotice(factory.Object, NullLogger<UnansweredQuestionNotice>.Instance),
            NullLogger<DeriveSpecHandler>.Instance);
    }

    private static DeriveSpecContext Context(params TicketComment[] comments)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.RunId, "run-2");
        var ticket = new Ticket(new TicketId("19106"), "Adopt the newest versions", Ticket, null, "open", "azdo", []);
        pipeline.Set(ContextKeys.Ticket, ticket);
        pipeline.Set<IReadOnlyList<TicketComment>>(ContextKeys.TicketComments, comments);
        return new DeriveSpecContext(
            ticket, new TrackerConnection { Type = TrackerType.AzureDevOps },
            [new RepoConnection { Name = "primary" }], new AgentConfig(), pipeline);
    }

    private static SpecSet PreviousQuestion() => new(
        "azdo-19106", [], SpecAccounting.Empty,
        [new SpecRevision(1, SpecRevisionCause.Initial, Asked.AddMinutes(-1))],
        SpecSource.BranchArtifact,
        new SpecHandback(SpecHandbackCase.Question, "reads two ways", Readings: [ReadingA, ReadingB], Taken: 0));

    private static TicketComment OurQuestion(DateTimeOffset at) => new(
        "agent-smith", at, SpecHandbackComment.Build(PreviousQuestion().Handback!, null, string.Empty));

    private static SpecDerivation QuestionAgain() => new(
        new SpecSet(
            "azdo-19106", [], SpecAccounting.Empty,
            [new SpecRevision(1, SpecRevisionCause.Initial, DateTimeOffset.UtcNow)], SpecSource.Derived,
            new SpecHandback(SpecHandbackCase.Question, "still reads two ways",
                Readings: ["conservative", "everything"], Taken: 1)),
        []);

    private static SpecDerivation PhasesCut(string goal = "Bump the packages")
    {
        var segments = TicketSegmenter.Segment(Ticket);
        var carries = segments.Select(s => s.Id).ToList();
        var phase = new SpecPhase(
            new Contracts.Models.PhaseDraft("p19106a", goal, "phase: p19106a", []) { Done = ["The packages are bumped."] },
            "bump-the-packages",
            SegmentExtractor.BuildMarkdown("p19106a", goal, carries, segments),
            carries);
        return new SpecDerivation(
            new SpecSet(
                "azdo-19106", [phase], SpecAccountingBuilder.Build([phase], [], segments),
                [new SpecRevision(1, SpecRevisionCause.Initial, DateTimeOffset.UtcNow)], SpecSource.Derived),
            []);
    }

    // Answers with the scripted derivation and remembers what the pipeline carried as the
    // pin at the moment of the call — the assertion is about ORDER: pin before model.
    private sealed class CapturingDeriver(SpecDerivation derivation) : ISpecSetDeriver
    {
        public string? PinSeen { get; private set; }

        public Task<(SpecDerivation? Derivation, string? Error)> DeriveAsync(
            Ticket ticket, IReadOnlyList<TicketSegment> segments, SpecSet? previous, string cause,
            AgentConfig agentConfig, PipelineContext pipeline, CancellationToken cancellationToken)
        {
            PinSeen = pipeline.TryGet<string>(ContextKeys.SpecQuestionPin, out var pin) ? pin : null;
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
}
