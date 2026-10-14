using System.Text.Json;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Persistence;
using AgentSmith.Application.Services.Prompts;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Triage;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Specs;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Specs.MultiRepo;

/// <summary>
/// 2026-10-12-f5f7f: a series whose repositories carry different revisions is handed back as its own
/// case — naming the lagging repositories and both ways out, before any model call, a resume included;
/// only a re-cut demand goes on, from every member's executed prefix. A member outside the checkout
/// fails the run naming it.
/// </summary>
public sealed class SeriesOutOfStepTests
{
    private static readonly string[] Both = ["Server", "Client"];
    private static readonly SpecHandback OutOfStep = SeriesOutOfStepComment.Handback(
        SeriesRepoBranches.Key, ["Client"], ["Server"]);
    private static readonly TrackerConnection Parkable = new()
    {
        Type = TrackerType.AzureDevOps, NeedsClarificationStatus = "needs-info", NotImplementableStatus = "blocked",
    };

    private readonly SeriesRepoBranches _branches = new();

    [Fact]
    public async Task SpecSourceResolver_LaggingRead_HandsBackOutOfStep_NoModel()
    {
        var read = await LaggingAsync();

        var plain = Decide(read, new PipelineContext());
        var resuming = new PipelineContext();
        resuming.Set(ContextKeys.ResumeCheckpoint, "cp");

        plain.NeedsModel.Should().BeFalse();
        plain.Handback!.Case.Should().Be(SpecHandbackCase.SeriesOutOfStep);
        plain.Handback.Reason.Should().Contain("Client").And.Contain("Server");
        Decide(read, resuming).Handback!.Case.Should().Be(SpecHandbackCase.SeriesOutOfStep,
            "nothing works from a mixed state, a resume included");
    }

    [Fact]
    public async Task SpecSourceResolver_NamedMemberNotCheckedOut_FailsRunNamingIt()
    {
        _branches.SubSeries("Server", SeriesRepoBranches.T2, Both, [Id('a')]);

        var decision = Decide(await _branches.ReadAsync("Server", Both), new PipelineContext());

        decision.Failure.Should().Contain("'Client'").And.Contain("not checked out");
        decision.Handback.Should().BeNull();
        decision.NeedsModel.Should().BeFalse();
    }

    [Fact]
    public async Task SpecRecutDemand_OutOfStep_ExecutedPrefixFromEveryMembersDone()
    {
        var read = await LaggingAsync();
        var demanded = new PipelineContext();
        demanded.Set<IReadOnlyList<TicketComment>>(ContextKeys.TicketComments,
            [new TicketComment("alice", DateTimeOffset.UtcNow, SpecSetComment.RecutDemand)]);

        var previous = SpecRecutDemand.PreviousOf(read.Read!);
        var decision = Decide(read, demanded);

        previous.Specs.Select(p => p.SpecId).Should().Equal(Id('a'), Id('b'), Id('c'));
        previous.Executed.Should().Equal(Id('a'), Id('b'));
        previous.ExecutedHead.Select(p => p.SpecId).Should().Equal(Id('a'), Id('b'));
        decision.Cause.Should().Be(SpecRevisionCause.RecutDemand, "a demand outranks the hand-back");
        decision.NeedsModel.Should().BeTrue();
        decision.Set!.Specs.Select(p => p.SpecId).Should().Equal(Id('a'), Id('b'), Id('c'));
    }

    [Fact]
    public void SpecHandbackComment_OutOfStep_NamesReposAndBothExits()
    {
        var body = SpecHandbackComment.Build(OutOfStep, null, TicketMention.NobodyToNotify);

        body.Should().Contain(SeriesOutOfStepComment.Marker).And.Contain("Client lags").And.Contain("Server carries");
        body.Should().Contain(SpecSetComment.RecutDemand).And.Contain("trigger status");
        body.Should().Contain("approve the specification again").And.Contain("file the ticket again");
        body.Should().NotContain(SpecHandbackComment.ContradictionMarker);
        OwnTicketComment.AwaitsAnswer(new TicketComment("agent-smith", DateTimeOffset.UtcNow, body)).Should().BeTrue();
    }

    [Fact]
    public async Task SpecHandbackHandler_OutOfStepWithoutTracker_Fails()
    {
        var result = await Handler(new Mock<ITicketProvider>(), new InMemorySpecSetPointerStore())
            .ExecuteAsync(new SpecHandbackContext(null, null, [], Pipeline()), default);

        result.IsSuccess.Should().BeFalse("continuing would run checkout over a mixed series");
        result.Message.Should().Contain("Client");
    }

    [Fact]
    public async Task SpecHandbackHandler_OutOfStep_ParksForClarification()
    {
        var tickets = new Mock<ITicketProvider>();
        var context = new SpecHandbackContext(
            new Ticket(new TicketId("4711"), "t", "d", null, "open", "azdo", []), Parkable, [], Pipeline());

        var result = await Handler(tickets, new InMemorySpecSetPointerStore()).ExecuteAsync(context, default);

        result.Message.Should().Contain("awaiting_user_input");
        tickets.Verify(t => t.FinalizeAsync(It.IsAny<TicketId>(),
            It.Is<string>(c => c.Contains(SeriesOutOfStepComment.Marker)), "needs-info",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("series_out_of_step")]
    [InlineData("7")]
    public void SpecDerivationEnvelope_ModelNamesOutOfStep_Refused(string name)
    {
        using var doc = JsonDocument.Parse($$$"""{"specs": [], "handback": {"case": "{{{name}}}", "reason": "x"}}""");

        new SpecDerivationEnvelope().Handback(doc.RootElement).Should().BeNull("only the code raises this case");
    }

    [Fact]
    public async Task SpecSetPointerRecorder_RecordShasAsync_MergesAndKeepsTheRevision()
    {
        var store = new InMemorySpecSetPointerStore();
        await store.SaveAsync("p", new SpecSetPointer("k", "Server", "s1", 3,
            RevisionShas: new Dictionary<string, string> { ["Server"] = "s1", ["Client"] = "c1" }), default);

        await new SpecSetPointerRecorder(store, NullLogger<SpecSetPointerRecorder>.Instance)
            .RecordShasAsync("p", "k", new Dictionary<string, string> { ["Server"] = "revert" }, default);

        var pointer = (await store.GetAsync("p", "k", default))!;
        pointer.RevisionNumber.Should().Be(3);
        pointer.RevisionSha.Should().Be("revert");
        pointer.ShaByRepo.Should().BeEquivalentTo(new Dictionary<string, string> { ["Server"] = "revert", ["Client"] = "c1" });
    }

    // Server is on the newest revision (a ran, c open); Client lags on an older one (b ran there).
    private async Task<SpecSetOnBranch> LaggingAsync()
    {
        _branches.SubSeries("Server", SeriesRepoBranches.T2, Both, [Id('a'), Id('c')], executed: Id('a'));
        _branches.SubSeries("Client", SeriesRepoBranches.T1, Both, [Id('b')], executed: Id('b'));
        var read = await _branches.ReadAsync("Server", Both);
        read.Read!.Lagging.Should().Equal("Client");
        return read;
    }

    private static SpecSourceResolver.Decision Decide(SpecSetOnBranch branch, PipelineContext pipeline) =>
        new SpecSourceResolver(new ApprovedSetHandoff(NullLogger<ApprovedSetHandoff>.Instance), AgentSmith.Tests.TestSupport.ApprovedSetDoubles.Continuation(),
                new FiledTicketSpecGate(NullLogger<FiledTicketSpecGate>.Instance), AgentSmith.Tests.TestSupport.SeriesSettingsDoubles.Reader(), NullLogger<SpecSourceResolver>.Instance)
            .Decide(branch, new Ticket(new TicketId("4711"), "t", "text", null, "open", "azdo", []),
                new SpecSetPointer(SeriesRepoBranches.Key, "Server", "sha-Server", 1), pipeline,
                SeriesRepoBranches.Key, TicketLabelVocabulary.ForOptional(null));

    private static string Id(char letter) => SeriesRepoBranches.Id(letter);

    private static PipelineContext Pipeline()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.SpecHandback, OutOfStep);
        return pipeline;
    }

    private static SpecHandbackHandler Handler(Mock<ITicketProvider> tickets, ISpecSetPointerStore pointers)
    {
        var factory = new Mock<ITicketProviderFactory>();
        factory.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(tickets.Object);
        return new SpecHandbackHandler(
            new SpecHandbackPark(factory.Object,
                new Application.Services.Lifecycle.UnmovedTicketReport(
                    new InMemoryUnmovedTicketStore(),
                    NullLogger<Application.Services.Lifecycle.UnmovedTicketReport>.Instance),
                Mock.Of<AgentSmith.Contracts.Reviews.IHandbackPrNotice>(), Mock.Of<AgentSmith.Contracts.Events.IEventPublisher>(), Mock.Of<AgentSmith.Contracts.Events.IRunContextAccessor>(), NullLogger<SpecHandbackPark>.Instance),
            new SpecParkStatusResolver(new ClarificationParkStatusResolver()), pointers,
            new SpecHandbackRepeat(NullLogger<SpecHandbackRepeat>.Instance),
            HandbackTestDoubles.Heads(), HandbackTestDoubles.Announcer(), NullLogger<SpecHandbackHandler>.Instance);
    }
}
