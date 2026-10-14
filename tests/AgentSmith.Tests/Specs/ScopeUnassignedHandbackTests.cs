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
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-10-12-d142: a ticket whose work cannot be divided between the repositories in scope
/// parks as its own case — its own comment, outside the contradiction machinery (no repeat
/// guard, no heads), raised by the code only.
/// </summary>
public sealed class ScopeUnassignedHandbackTests
{
    private static readonly SpecHandback Unassigned = SpecScopeHandback.For(
        "azdo-1", "2026-10-12-0a0a", SpecSource.Derived, ["BackgroundWorker", "Server"]).Handback!;

    private static readonly TrackerConnection Parkable = new()
    {
        Type = TrackerType.AzureDevOps, NeedsClarificationStatus = "needs-info", NotImplementableStatus = "blocked",
    };

    [Fact]
    public void SpecScopeHandback_MultiRepo_IsScopeUnassigned()
    {
        Unassigned.Case.Should().Be(SpecHandbackCase.ScopeUnassigned);
        Unassigned.Reason.Should().Contain("BackgroundWorker, Server");
    }

    [Fact]
    public void SpecHandbackComment_ScopeUnassigned_NotTheContradictionText()
    {
        var body = SpecHandbackComment.Build(Unassigned, null, TicketMention.NobodyToNotify);

        body.Should().NotContain(SpecHandbackComment.ContradictionMarker);
        body.Should().Contain(SpecHandbackComment.ScopeUnassignedMarker).And.Contain(Unassigned.Reason);
    }

    [Fact]
    public void SpecHandbackRepeat_ScopeUnassigned_NeverARepeat()
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyList<TicketComment>>(ContextKeys.TicketComments, [OurComment()]);

        new SpecHandbackRepeat(NullLogger<SpecHandbackRepeat>.Instance).IsRepeat(
                new SpecSetPointer("azdo-1", "primary", "sha", 1, SpecHandbackCase.ScopeUnassigned, 3),
                SpecHandbackCase.ScopeUnassigned, pipeline)
            .Should().BeFalse("every re-entry needs a person; the park leaves the trigger status");
    }

    [Fact]
    public void SpecParkStatusResolver_ScopeUnassigned_Clarification() =>
        new SpecParkStatusResolver(new ClarificationParkStatusResolver())
            .TryResolve(new PipelineContext(), Parkable, SpecHandbackCase.ScopeUnassigned)
            .Should().Be("needs-info");

    [Fact]
    public void TicketConversation_UnansweredScopeUnassigned_KeptForNextRun()
    {
        var comment = OurComment();

        OwnTicketComment.AwaitsAnswer(comment).Should().BeTrue();
        TicketConversationPromptSection.Render([comment])
            .Should().Contain(SpecHandbackComment.ScopeUnassignedMarker);
    }

    [Fact]
    public async Task SpecHandbackHandler_ScopeUnassignedWithoutTracker_Fails()
    {
        var result = await Handler(new Mock<ITicketProvider>(), new InMemorySpecSetPointerStore())
            .ExecuteAsync(new SpecHandbackContext(null, null, [], Pipeline()), default);

        result.IsSuccess.Should().BeFalse("continuing would run checkout with no spec to work");
        result.Message.Should().Contain("BackgroundWorker, Server");
    }

    [Fact]
    public async Task SpecHandbackHandler_ScopeUnassigned_ParksForClarificationWithoutHeads()
    {
        var tickets = new Mock<ITicketProvider>();
        var pointers = new InMemorySpecSetPointerStore();
        await pointers.SaveAsync(string.Empty, new SpecSetPointer("azdo-1", "primary", "sha", 1), default);
        var context = new SpecHandbackContext(
            new Ticket(new TicketId("1"), "t", "d", null, "open", "azdo", []), Parkable, [], Pipeline());

        var result = await Handler(tickets, pointers).ExecuteAsync(context, default);

        result.Message.Should().Contain("awaiting_user_input");
        tickets.Verify(t => t.FinalizeAsync(It.IsAny<TicketId>(),
            It.Is<string>(c => c.Contains(SpecHandbackComment.ScopeUnassignedMarker)), "needs-info",
            It.IsAny<CancellationToken>()), Times.Once);
        var pointer = await pointers.GetAsync(string.Empty, "azdo-1", default);
        pointer!.LastHandbackCase.Should().Be(SpecHandbackCase.ScopeUnassigned);
        pointer.HandbackHeads.Should().BeNull("only a contradiction stores the heads it parked at");
    }

    [Theory]
    [InlineData("scope_unassigned")]
    [InlineData("ScopeUnassigned")]
    [InlineData("6")]
    public void SpecDerivationEnvelope_ModelNamesScopeUnassigned_Refused(string name)
    {
        using var doc = JsonDocument.Parse(
            $$$"""{"specs": [], "handback": {"case": "{{{name}}}", "reason": "cannot divide"}}""");
        var envelope = new SpecDerivationEnvelope();

        envelope.Handback(doc.RootElement).Should().BeNull("only the code raises this case");
        envelope.HandbackRejection(doc.RootElement).Should().Contain("raised by the framework");
    }

    private static TicketComment OurComment() => new(
        "agent-smith", DateTimeOffset.UtcNow.AddHours(-1),
        SpecHandbackComment.Build(Unassigned, null, TicketMention.NobodyToNotify));

    private static PipelineContext Pipeline()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.SpecHandback, Unassigned);
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
            AgentSmith.Tests.TestSupport.HandbackTestDoubles.Heads(),
            AgentSmith.Tests.TestSupport.HandbackTestDoubles.Announcer(), NullLogger<SpecHandbackHandler>.Instance);
    }
}
