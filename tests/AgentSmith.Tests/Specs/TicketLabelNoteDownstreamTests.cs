using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Prompts;
using AgentSmith.Application.Services.Scope;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Validation;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-09-18-d518: the note is stripped at the DOOR, so nothing downstream carries a strip of
/// its own. These tests hold the readers the door protects — the prompts (the master, the scope
/// classifier, the derivation, the unfenced cut review and the epic ground), the whole-ticket
/// fallback that pins a description AS the ratified phase, the pull-request body, the ticket
/// fingerprint and the acceptance-criteria scan.
/// </summary>
public sealed class TicketLabelNoteDownstreamTests
{
    private const string Body = "## Goal\nMake the widget storable.\n\n"
        + "## Acceptance criteria\n- the widget is stored\n";

    private static readonly string Note =
        TicketLabelNote.For([FiledTicketLabels.ApprovedSetStamp])!;

    /// <summary>The ticket as the fetch door publishes it: its own text, without our note.</summary>
    private static Ticket Fetched() =>
        TicketLabelNoteStripper.Strip(
            new Ticket(new TicketId("42"), "Widget storage", Body + Note, null, "open", "test"));

    [Fact]
    public async Task Prompts_TheMasterTheClassifierTheDerivationTheCutReviewAndTheEpicGround_DoNotSeeTheNote()
    {
        var ticket = Fetched();
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.EpicGround, new EpicGround(
            "41", "Widget platform", TicketLabelNoteStripper.Strip("## Goal\nOne platform.\n" + Note)));
        var segments = TicketSegmenter.Segment(ticket.Description);

        string[] prompts =
        [
            MasterUserPrompt.Build(
                ticket, new Repository(new BranchName("run/42"), "https://git.test/app"), [], "", ""),
            await ClassifierPromptAsync(ticket),
            SpecPromptComposer.Compose(ticket, segments, previous: null, cause: "", pipeline),
            SpecCutReviewPrompt.For([Draft()], SpecCutReviewTicketText.Of(ticket), look: null),
            EpicGroundPromptSection.Build(pipeline),
        ];

        prompts.Should().HaveCount(5).And.OnlyContain(
            p => !p.Contains(TicketLabelNote.BeginIdentifier, StringComparison.Ordinal)
                && !p.Contains(TicketLabelNote.Heading, StringComparison.Ordinal)
                && !p.Contains("binds this ticket to phase execution", StringComparison.Ordinal));
        prompts.Should().OnlyContain(p => p.Contains("widget", StringComparison.OrdinalIgnoreCase),
            "the ticket itself still reaches every one of them");
    }

    [Fact]
    public void Fallback_TheWholeTicketSpec_DoesNotCarryTheNote()
    {
        // The whole-ticket fallback pins the entire description AS the ratified phase markdown,
        // so a leak here is not a prompt hazard — it is the specification.
        var ticket = Fetched();
        var fallback = new SpecFallback(
            new SpecDraftValidator(new PhaseSpecSchemaProvider()),
            new PhaseDraftReader(), new DerivedPhaseYamlRenderer());

        var set = fallback.Build(
            "key", ticket, TicketSegmenter.Segment(ticket.Description), [], SpecSource.Derived);

        var markdown = set.Phases.Single().Markdown;
        markdown.Should().Contain("Make the widget storable")
            .And.NotContain(TicketLabelNote.BeginIdentifier)
            .And.NotContain(TicketLabelNote.Heading);
    }

    [Fact]
    public void PullRequest_TheBody_DoesNotCarryTheNote()
    {
        // CommitAndPRHandler leads the pull-request body with the run ticket's description,
        // verbatim — the ticket it reads is the one the fetch door published.
        var ticket = Fetched();

        ticket.Description.Should().Contain("Make the widget storable")
            .And.NotContain(TicketLabelNote.BeginIdentifier)
            .And.NotContain(TicketLabelNote.Heading)
            .And.NotContain("Removing it costs");
    }

    [Fact]
    public void Fingerprint_TheNote_DoesNotCountAsATicketChange()
    {
        // Stripped first, the note never reads as an edit — which is right: the framework wrote
        // it, and a ticket whose only difference is our own note was edited by nobody.
        var withNote = new Ticket(new TicketId("42"), "Widget storage", Body + Note, null, "open", "test");
        var never = new Ticket(new TicketId("42"), "Widget storage", Body, null, "open", "test");

        TicketTextFingerprint.Of(TicketLabelNoteStripper.Strip(withNote))
            .Should().Be(TicketTextFingerprint.Of(TicketLabelNoteStripper.Strip(never)));
    }

    [Fact]
    public void Criteria_TheNoteUnderItsHeading_IsNotReadAsAnAcceptanceCriterion()
    {
        // The scan breaks on any heading and takes only marker lines: the note's own heading
        // ends it, and bare prose under that heading cannot be mistaken for a criterion.
        var filed = new PhaseTicketRenderer().RenderPhase(Draft(), "job-1", Note).Body;

        filed.Should().Contain(TicketLabelNote.Heading, "the note is on the ticket this reads");
        AcceptanceCriteriaSection.Read(filed).Should().Equal("the widget is stored");
    }

    private static async Task<string> ClassifierPromptAsync(Ticket ticket)
    {
        var chat = new StubChatClient(new Queue<string>(["""{"repos": []}"""]));
        var classifier = new RepoScopeClassifier(
            new StubChatClientFactory(chat), EventTestStubs.RunContext,
            NullLogger<RepoScopeClassifier>.Instance);

        await classifier.ClassifyAsync(
            ticket, comments: null, [new RepoConnection { Name = "app" }],
            new Dictionary<string, IReadOnlyList<RemoteContextDiscovery>>(StringComparer.Ordinal),
            new AgentConfig(), new PipelineContext(), CancellationToken.None);

        return string.Join("\n", chat.LastMessages.Select(m => m.Text));
    }

    private static PhaseDraft Draft() =>
        new("p9000a", "Widget storage",
            "phase: p9000a\ngoal: \"Widget storage\"\ndone:\n  - \"the widget is stored\"",
            []) { Done = ["the widget is stored"] };
}
