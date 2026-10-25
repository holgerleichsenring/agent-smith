using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Events;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.Handlers;
using AgentSmith.Tests.Specs;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Decisions;

/// <summary>
/// 2026-10-13-8762o: decide_by_default answers in the same call and puts event 88 on the run; the
/// spec-execution prompt names the tool. 2026-10-25-9324e: remember publishes no pointer row any more
/// (MemoryNoRatificationTests).
/// </summary>
public sealed class DefaultDecisionToolTests
{
    private const string RunId = "2026-10-14T1100-defaults";

    [Fact]
    public async Task DecideByDefault_ReturnsAtOnce_AndPublishesEvent88()
    {
        var events = new RecordingEventPublisher();
        var host = new DefaultDecisionToolHost(Recorder(events));

        var answer = await host.DecideByDefault("Which queue?", "the existing bus", ["a new queue", "polling"],
            "the bus already carries run events", holds_series: true);

        answer.Should().StartWith("Taken by default, recorded for ratification").And.Contain("the existing bus");
        var taken = events.Events.Should().ContainSingle().Which.Should().BeOfType<DefaultDecisionTakenEvent>().Subject;
        taken.Type.Should().Be(EventType.DefaultDecisionTaken);
        ((int)taken.Type).Should().Be(88);
        (taken.RunId, taken.Question, taken.Chosen, taken.Why, taken.HoldsSeries, taken.Kind, taken.PointerPath)
            .Should().Be((RunId, "Which queue?", "the existing bus", "the bus already carries run events", true,
                DefaultDecisionKinds.Decision, (string?)null));
        taken.Alternatives.Should().Equal("a new queue", "polling");
    }

    [Fact]
    public async Task DecideByDefault_PublishFails_AnswersNotRecorded()
    {
        var host = new DefaultDecisionToolHost(Recorder(new ThrowingPublisher()));

        var answer = await host.DecideByDefault("Which queue?", "the existing bus", ["a new queue"], "it exists");

        answer.Should().StartWith("NOT recorded").And.Contain("the existing bus").And.Contain("ask_human");
    }

    [Fact]
    public async Task DecideByDefault_OverBounds_IsRefusedByName()
    {
        var events = new RecordingEventPublisher();
        var host = new DefaultDecisionToolHost(Recorder(events));

        var answer = await host.DecideByDefault(new string('q', 501), "", [.. Enumerable.Repeat("x", 9)],
            new string('w', 1001));
        var longAlternative = await host.DecideByDefault("q", "c", [new string('a', 301)], "w");
        var none = await host.DecideByDefault("q", "c", [], "w");

        answer.Should().StartWith("Error: decide_by_default refused, nothing was recorded")
            .And.Contain("'question' is 501 characters, at most 500").And.Contain("'chosen' is required")
            .And.Contain("'why' is 1001 characters, at most 1000").And.Contain("'alternatives' has 9 entries");
        longAlternative.Should().Contain("'alternatives entry 1' is 301 characters, at most 300");
        none.Should().Contain("'alternatives' has 0 entries");
        events.Events.Should().BeEmpty();
    }

    [Fact]
    public async Task DecideByDefault_CarriesTheCurrentSpecId()
    {
        var events = new RecordingEventPublisher();
        var pipeline = Pipeline();
        pipeline.Set(ContextKeys.CurrentSpec, SpecBuildNoteTests.Draft("2026-10-14-5678a"));

        var records = MasterHandlerFixture.RecordHosts(new MasterHandlerFixture.NoOpDecisionLogger(), events)
            .Create(pipeline, "/work");
        await records.DefaultDecision!.DecideByDefault("q", "c", ["d"], "w");

        events.Events.OfType<DefaultDecisionTakenEvent>().Single()
            .Should().Match<DefaultDecisionTakenEvent>(e => e.SpecId == "2026-10-14-5678a" && e.Conversation == null);
    }

    [Fact]
    public async Task DecideByDefault_InADesignTurn_RowCarriesTheConversation()
    {
        var events = new RecordingEventPublisher();
        var pipeline = Pipeline();
        pipeline.Set(ContextKeys.DialogueJobId, "conversation-7");

        var records = MasterHandlerFixture.RecordHosts(new MasterHandlerFixture.NoOpDecisionLogger(), events)
            .Create(pipeline, "/work");
        await records.DefaultDecision!.DecideByDefault("q", "c", ["d"], "w");

        var taken = events.Events.OfType<DefaultDecisionTakenEvent>().Single();
        (taken.RunId, taken.Conversation, taken.SpecId).Should().Be((RunId, "conversation-7", (string?)null));
        MasterHandlerFixture.RecordHosts(new MasterHandlerFixture.NoOpDecisionLogger(), events)
            .Create(new PipelineContext(), "/work").DefaultDecision.Should().BeNull("no run id, nothing to record on");
    }

    [Fact]
    public void SpecExecutionPrompt_NoLongerSaysLogEveryChoiceUnqualified()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.CurrentSpec, SpecBuildNoteTests.Draft("2026-10-14-5678a"));

        var prompt = new SpecExecutionPromptFactory().Build(pipeline,
            new Ticket(new TicketId("7"), "t", "d", null, "open", "test"),
            new Repository(new BranchName("main"), "https://example.test/shop.git"), ["shop"], string.Empty, string.Empty);

        prompt.Should().NotContain("Log every non-obvious choice")
            .And.Contain("Take a choice a person might overrule with decide_by_default; log every other non-obvious choice via log_decision");
    }

    private static DefaultDecisionRecorder Recorder(IEventPublisher events) =>
        new(events, RunId, null, null, NullLogger.Instance);

    private static PipelineContext Pipeline()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.RunId, RunId);
        return pipeline;
    }

    private sealed class ThrowingPublisher : IEventPublisher
    {
        public Task PublishAsync(RunEvent runEvent, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("the bus is down");
    }
}
