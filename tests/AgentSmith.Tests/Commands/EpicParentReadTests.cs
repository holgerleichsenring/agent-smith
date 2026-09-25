using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Commands;

/// <summary>
/// 2026-09-25-d0b8: the epic-parent read is ONE service with more than one caller. It used to
/// be reachable only from a pipeline — it wrote its result into a pipeline context and returned
/// a run step's sentence — so a caller that owns no pipeline could not ask the question without
/// fabricating one. These tests pin that the pipeline-free caller and the run's fetch step get
/// the same ground and the same degradation, and that the step publishes exactly what it did.
/// </summary>
public sealed class EpicParentReadTests
{
    private const string ParentBody = "## Goal\nOne vocabulary for the widget service.\n";
    private const string Stamped = "4711";

    private readonly Mock<ITicketProvider> _provider = new();
    private readonly EpicParentReader _reader =
        new(NullLogger<EpicParentReader>.Instance);

    [Fact]
    public async Task EpicParent_ATicketWithAStamp_IsReadTheSameWayByARunAndADirectCaller()
    {
        Answers(Parent());
        var child = Child(FiledTicketLabels.ParentStamp(Stamped));

        var direct = await _reader.ReadAsync(_provider.Object, child, CancellationToken.None);
        var pipeline = new PipelineContext();
        await Step().AttachAsync(_provider.Object, child, pipeline, CancellationToken.None);

        direct.Ground.Should().Be(new EpicGround(Stamped, "Widget service", ParentBody));
        pipeline.Get<EpicGround>(ContextKeys.EpicGround).Should().Be(direct.Ground,
            "one read answers both callers - a second answer would be a second rule");
    }

    [Fact]
    public async Task EpicParent_AParentThatCannotBeOpened_DegradesToANoteInBoth()
    {
        Answers(new HttpRequestException("404 - the epic was deleted"));
        var child = Child(FiledTicketLabels.ParentStamp(Stamped));

        var direct = await _reader.ReadAsync(_provider.Object, child, CancellationToken.None);
        var pipeline = new PipelineContext();
        var step = await Step().AttachAsync(
            _provider.Object, child, pipeline, CancellationToken.None);

        direct.Ground.Should().BeNull("a parent that is gone leaves the child's ticket complete");
        direct.Note.Should().Contain(Stamped).And.Contain("could not be read");
        step.Should().EndWith(direct.Note, "both callers name the same failure for one reason");
        pipeline.TryGet<EpicGround>(ContextKeys.EpicGround, out _).Should().BeFalse();
    }

    [Fact]
    public async Task EpicParent_ATicketWithNoStamp_ReadsNothing()
    {
        Answers(Parent());

        var direct = await _reader.ReadAsync(
            _provider.Object, Child("bug"), CancellationToken.None);

        direct.Should().Be(EpicParentRead.Unstamped);
        _provider.Verify(
            p => p.GetTicketAsync(It.IsAny<TicketId>(), It.IsAny<CancellationToken>()),
            Times.Never, "a ticket that is nobody's slice addresses no parent");
    }

    [Fact]
    public async Task EpicParent_ThePipelineStep_PublishesExactlyWhatItPublishedBefore()
    {
        Answers(Parent());
        var pipeline = new PipelineContext();

        var note = await Step().AttachAsync(
            _provider.Object, Child(FiledTicketLabels.ParentStamp(Stamped)), pipeline,
            CancellationToken.None);
        var unstamped = new PipelineContext();
        var silence = await Step().AttachAsync(
            _provider.Object, Child("bug"), unstamped, CancellationToken.None);

        note.Should().Be($" — epic ground read from parent {Stamped}",
            "the fetch step's message is appended verbatim to the run's step output");
        pipeline.Get<EpicGround>(ContextKeys.EpicGround)
            .Should().Be(new EpicGround(Stamped, "Widget service", ParentBody));
        silence.Should().BeEmpty("a run outside an epic reads exactly as it always did");
        unstamped.TryGet<EpicGround>(ContextKeys.EpicGround, out _).Should().BeFalse();
    }

    private EpicGroundFetcher Step() => new(_reader);

    private void Answers(Ticket parent) => _provider.Setup(p => p.GetTicketAsync(
            It.Is<TicketId>(t => t.Value == Stamped), It.IsAny<CancellationToken>()))
        .ReturnsAsync(parent);

    private void Answers(Exception failure) => _provider.Setup(p => p.GetTicketAsync(
            It.Is<TicketId>(t => t.Value == Stamped), It.IsAny<CancellationToken>()))
        .ThrowsAsync(failure);

    private static Ticket Child(params string[] labels) =>
        new(new TicketId("42"), "Widget storage", "The table and the repository.",
            null, "Open", "test", labels);

    private static Ticket Parent() =>
        new(new TicketId(Stamped), "Widget service", ParentBody, null, "Open", "test",
            ["phase-epic"]);
}
