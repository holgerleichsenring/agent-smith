using System.Text.Json;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Providers.Tickets;
using FluentAssertions;
using Markdig;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Commands;

/// <summary>
/// 2026-09-18-d518: the ticket fetch is the DOOR every reader of ticket-origin text comes
/// through. The framework's own label note comes off there — on the ticket, and on the epic
/// parent the same step fetches as a second ticket — in all three encodings a description
/// arrives in, so no reader downstream needs a strip of its own.
/// </summary>
public sealed class TicketLabelNoteFetchTests
{
    private const string Requirement = "## Goal\nMake the widget storable.\n";

    private static readonly string Note =
        TicketLabelNote.For([FiledTicketLabels.ApprovedSetStamp])!;

    [Fact]
    public async Task Fetch_ATicketCarryingTheNote_PublishesADescriptionWithoutIt()
    {
        var published = await FetchAsync(Requirement + Note);

        published.Description.Should().Be(Requirement.TrimEnd())
            .And.NotContain(TicketLabelNote.BeginIdentifier)
            .And.NotContain(TicketLabelNote.Heading);
    }

    [Fact]
    public async Task Fetch_AnAzureDevOpsTicketWhoseBodyIsHtml_IsStrippedToo()
    {
        // Azure DevOps converts the markdown we file to HTML on create and hands that back:
        // the note's INNER content changes shape (the heading becomes an element with a
        // generated identifier) and the markers ride through unchanged.
        var html = Markdown.ToHtml(
            Requirement + Note, new MarkdownPipelineBuilder().UseAdvancedExtensions().Build());
        html.Should().Contain(TicketLabelNote.BeginIdentifier, "the comment survives the conversion");

        var published = await FetchAsync(html);

        published.Description.Should().NotContain(TicketLabelNote.BeginIdentifier)
            .And.NotContain("What these labels bind")
            .And.Contain("Make the widget storable", "only the note is taken");
    }

    [Fact]
    public async Task Fetch_AJiraTicketWhoseBodyRoundTrippedThroughItsDocumentFormat_IsStrippedToo()
    {
        // Jira stores the description as its own structured document, one paragraph per line,
        // and renders HTML as literal text — so the operator SEES the delimiters there, and
        // the strip still works because the marker's characters round-trip unchanged.
        var stored = RoundTripThroughJira(Requirement + Note);
        stored.Should().Contain(TicketLabelNote.Begin).And.Contain(TicketLabelNote.End);

        var published = await FetchAsync(stored);

        published.Description.Should().NotContain(TicketLabelNote.BeginIdentifier)
            .And.NotContain(TicketLabelNote.Heading);
    }

    [Fact]
    public async Task Fetch_AnEpicParentCarryingTheNote_PublishesAGroundWithoutIt()
    {
        // The epic parent is a SECOND ticket, fetched and published by the same step — and this
        // phase gives it a note too, so the ground would otherwise carry one into the derivation.
        var pipeline = await FetchPipelineAsync(
            Requirement, parentBody: "## Goal\nOne platform.\n" + Note);

        var ground = pipeline.Get<EpicGround>(ContextKeys.EpicGround);
        ground.Body.Should().Contain("One platform.")
            .And.NotContain(TicketLabelNote.BeginIdentifier)
            .And.NotContain(TicketLabelNote.Heading);
    }

    [Fact]
    public async Task Fetch_ANoteMissingOneMarker_IsLeftAloneAsOrdinaryProse()
    {
        // An operator can edit the body, so one marker can survive its partner. The stripper
        // takes only a COMPLETE pair; the remnant stays as ticket prose. That is an accepted
        // leak, not a case to handle with a heuristic over operator text.
        var halfDeleted = Requirement + Note.Replace(TicketLabelNote.End, string.Empty, StringComparison.Ordinal);

        var published = await FetchAsync(halfDeleted);

        published.Description.Should().Contain(TicketLabelNote.BeginIdentifier)
            .And.Contain(TicketLabelNote.Heading);
    }

    [Fact]
    public async Task Fetch_ABeginMarkerWhoseSentenceWasEdited_IsStillStripped()
    {
        // The explaining sentence is DISPLAY TEXT — visible and editable on Jira. Only the
        // identifier is the contract, so a reworded (or translated) sentence still strips.
        var reworded = Requirement
            + "<!-- " + TicketLabelNote.BeginIdentifier + " (bookkeeping > ours, please leave it) -->\n"
            + TicketLabelNote.Heading + "\nThe `phase` label binds this ticket.\n"
            + TicketLabelNote.End + "\n";

        var published = await FetchAsync(reworded);

        published.Description.Should().Be(Requirement.TrimEnd(),
            "the match runs to the comment's close, so even a `>` in the sentence is tolerated");
    }

    [Fact]
    public async Task Fetch_ATicketWithoutTheNote_IsPublishedUnchanged()
    {
        var body = Requirement + "## Notes\nA person wrote this, arrows and all: -->\n";

        var published = await FetchAsync(body);

        published.Description.Should().Be(body, "text carrying no complete pair is not touched");
    }

    /// <summary>The ticket as the fetch handler publishes it onto the pipeline.</summary>
    private static async Task<Ticket> FetchAsync(string description) =>
        (await FetchPipelineAsync(description)).Get<Ticket>(ContextKeys.Ticket);

    private static async Task<PipelineContext> FetchPipelineAsync(
        string description, string? parentBody = null)
    {
        var ticketId = new TicketId("42");
        var labels = parentBody is null ? [] : new[] { FiledTicketLabels.ParentStamp("41") };
        var provider = new Mock<ITicketProvider>();
        provider.Setup(p => p.GetTicketAsync(ticketId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Ticket(ticketId, "Widget storage", description, null, "open", "test", labels));
        provider.Setup(p => p.GetTicketAsync(new TicketId("41"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Ticket(
                new TicketId("41"), "Widget platform", parentBody ?? string.Empty, null, "open", "test"));
        var factory = new Mock<ITicketProviderFactory>();
        factory.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(provider.Object);
        var handler = new FetchTicketHandler(
            factory.Object, Mock.Of<IEventPublisher>(), Mock.Of<IRunContextAccessor>(),
            new TicketExtrasFetcher(NullLogger<TicketExtrasFetcher>.Instance),
            new EpicGroundFetcher(new EpicParentReader(NullLogger<EpicParentReader>.Instance)),
            NullLogger<FetchTicketHandler>.Instance);

        var pipeline = new PipelineContext();
        var result = await handler.ExecuteAsync(
            new FetchTicketContext(ticketId, new TrackerConnection { Type = TrackerType.GitHub }, pipeline),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        return pipeline;
    }

    /// <summary>What Jira stores and hands back: one paragraph per line, parsed to text again.</summary>
    private static string RoundTripThroughJira(string markdown)
    {
        var document = JsonSerializer.SerializeToDocument(JiraAdfRenderer.FromMultilineText(markdown));
        return JiraAdfParser.ExtractText(document.RootElement);
    }
}
