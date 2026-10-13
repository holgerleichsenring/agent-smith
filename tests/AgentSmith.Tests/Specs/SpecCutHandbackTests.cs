using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Persistence;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Triage;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestHelpers;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-10-13-9a59e: every hand-back leaves its case on a published cut — through the publisher for a
/// derived set, through the hand-back step for a scope refusal or a missing spec that never reached it —
/// and a set the publisher already announced is not announced twice.
/// </summary>
public sealed class SpecCutHandbackTests
{
    private const string RunId = "2026-10-13T0900-hhhh";
    private static readonly DateTimeOffset At = new(2026, 10, 13, 9, 0, 0, TimeSpan.Zero);
    private static readonly TrackerConnection Parkable = new()
    {
        Type = TrackerType.AzureDevOps, NeedsClarificationStatus = "needs-info", NotImplementableStatus = "blocked",
    };

    [Fact]
    public async Task SpecCut_HandedBackSet_IsPublishedWithItsCase()
    {
        var events = EventTestStubs.Recording();
        var set = Set(new SpecHandback(SpecHandbackCase.NotImplementable, "cannot be built"));

        await HandbackTestDoubles.Announcer(events).AnnounceAsync(Pipeline(), set, committed: true, CancellationToken.None);

        var cut = events.Events.OfType<SpecSetPublishedEvent>().Single();
        (cut.SetKey, cut.HandbackCase).Should().Be(("azdo-1", "NotImplementable"));
        cut.Specs.Should().BeEmpty();
    }

    [Fact]
    public async Task SpecCut_DerivedSet_CarriesEverySpecAsPublished()
    {
        var events = EventTestStubs.Recording();
        var draft = new SpecDraft("2026-10-13-1234a", "Add the endpoint", "spec: a\n", [])
        {
            Done = ["it builds"], Steps = [new SpecStep("s1", "Add it", "api:src/A.cs")], Repos = ["api"],
        };
        var set = Set(null) with { Specs = [new DerivedSpec(draft, "endpoint", string.Empty, [])] };

        await HandbackTestDoubles.Announcer(events).AnnounceAsync(Pipeline(), set, committed: true, CancellationToken.None);

        var cut = events.Events.OfType<SpecSetPublishedEvent>().Single();
        cut.HandbackCase.Should().BeNull();
        cut.Specs.Should().Equal(new SpecCutEntry("2026-10-13-1234a", 1, "2026-10-13-1234a-endpoint",
            "Add the endpoint", draft.Repos, draft.Steps, draft.Done, "spec: a\n"));
    }

    [Fact]
    public async Task SpecCut_ScopeRefusal_RecordsTheRefusedCase()
    {
        var events = EventTestStubs.Recording();
        var pipeline = Pipeline();
        pipeline.Set(ContextKeys.SpecHandback, new SpecHandback(SpecHandbackCase.Refused, "must not", Quote: "drop the table"));

        await Handler(events).ExecuteAsync(Context(pipeline), CancellationToken.None);

        var cut = events.Events.OfType<SpecSetPublishedEvent>().Single();
        (cut.RunId, cut.SetKey, cut.HandbackCase, cut.RevisionNumber).Should().Be((RunId, "azdo-1", "Refused", (int?)null));
        cut.Specs.Should().BeEmpty();
    }

    [Fact]
    public async Task SpecCut_MissingSpec_RecordsItsCase()
    {
        var events = EventTestStubs.Recording();
        var pipeline = Pipeline();
        pipeline.Set(ContextKeys.SpecHandback,
            new SpecHandback(SpecHandbackCase.SpecificationMissingFromBranch, "the approved specs are not on the branch"));

        await Handler(events).ExecuteAsync(Context(pipeline), CancellationToken.None);

        events.Events.OfType<SpecSetPublishedEvent>().Single().HandbackCase.Should().Be("SpecificationMissingFromBranch");
    }

    [Fact]
    public async Task SpecHandback_SetAlreadyHandedBack_PublishesNoSecondCut()
    {
        var events = EventTestStubs.Recording();
        var handback = new SpecHandback(SpecHandbackCase.Question, "reads two ways", Readings: ["a", "b"]);
        var pipeline = Pipeline();
        pipeline.Set(ContextKeys.SpecSet, Set(handback));
        pipeline.Set(ContextKeys.SpecHandback, handback);

        var result = await Handler(events).ExecuteAsync(Context(pipeline), CancellationToken.None);

        result.Message.Should().Contain("awaiting_user_input");
        events.Events.OfType<SpecSetPublishedEvent>().Should().BeEmpty("the publisher announced that set");
    }

    private static SpecSet Set(SpecHandback? handback) => new(
        "azdo-1", [], SpecAccounting.Empty, [new SpecRevision(1, "initial", At)], SpecSource.Derived, handback);

    private static PipelineContext Pipeline()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.RunId, RunId);
        return pipeline;
    }

    private static SpecHandbackContext Context(PipelineContext pipeline) =>
        new(new Ticket(new TicketId("1"), "t", "d", null, "open", "azdo", []), Parkable, [], pipeline);

    private static SpecHandbackHandler Handler(IEventPublisher events)
    {
        var factory = new Mock<ITicketProviderFactory>();
        factory.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(new Mock<ITicketProvider>().Object);
        return new SpecHandbackHandler(
            new SpecHandbackPark(factory.Object,
                new Application.Services.Lifecycle.UnmovedTicketReport(
                    new InMemoryUnmovedTicketStore(),
                    NullLogger<Application.Services.Lifecycle.UnmovedTicketReport>.Instance),
                Mock.Of<AgentSmith.Contracts.Reviews.IHandbackPrNotice>(), NullLogger<SpecHandbackPark>.Instance),
            new SpecParkStatusResolver(new ClarificationParkStatusResolver()), new InMemorySpecSetPointerStore(),
            new SpecHandbackRepeat(NullLogger<SpecHandbackRepeat>.Instance),
            HandbackTestDoubles.Heads(), HandbackTestDoubles.Announcer(events), NullLogger<SpecHandbackHandler>.Instance);
    }
}
