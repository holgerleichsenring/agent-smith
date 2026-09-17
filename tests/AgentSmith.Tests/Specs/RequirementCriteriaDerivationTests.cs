using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Validation;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-09-17-042eb: GitHub, GitLab and Jira have no acceptance field, so a requirement ticket
/// states its criteria in its body. Both deriver paths read that section when the field is empty:
/// the prompt's criteria line would otherwise say "None specified" above them, and the fallback
/// would build a phase with no done list.
/// </summary>
public sealed class RequirementCriteriaDerivationTests
{
    private const string Yaml = """
        phase: p9000a
        goal: Widget storage layer
        done:
          - "the table exists"
          - "the repository reads it back"
        """;

    private static readonly string Body = new PhaseTicketRenderer()
        .RenderChildRequirement(new PhaseDraft("p9000a", "Widget storage layer", Yaml, []), new HashSet<string>()).Body;

    [Fact]
    public void SpecPrompt_TicketWithoutAcceptanceField_ReadsTheBodySection()
    {
        var ticket = new Ticket(new TicketId("42"), "p9000a: Widget storage layer", Body, null, "open", "test");

        var prompt = SpecPromptComposer.Compose(
            ticket, TicketSegmenter.Segment(Body), previous: null, cause: string.Empty, new PipelineContext());

        prompt.Should().Contain("**Acceptance Criteria:** \n- the table exists\n- the repository reads it back");
        prompt.Should().NotContain("None specified");
    }

    [Fact]
    public void SpecFallback_RequirementTicket_TakesDoneFromTheCriteriaSection()
    {
        var ticket = new Ticket(new TicketId("42"), "p9000a: Widget storage layer", Body, null, "open", "test");
        var validator = new SpecDraftValidator(new PhaseSpecSchemaProvider());
        var fallback = new SpecFallback(validator, new PhaseDraftReader(), new DerivedPhaseYamlRenderer());

        var set = fallback.Build("key", ticket, TicketSegmenter.Segment(Body), [], SpecSource.Derived);

        set.Phases.Single().Draft.Done.Should().Equal("the table exists", "the repository reads it back");
    }

    [Fact]
    public void AcceptanceCriteriaSection_HtmlBody_ReadsTheItems()
    {
        const string html = """
            <h2 id="goal">Goal</h2>
            <p>Widget storage layer</p>
            <h2 id="acceptance-criteria">Acceptance criteria</h2>
            <ul>
            <li>the table exists</li>
            <li>the repository reads it &amp; writes it</li>
            </ul>
            <h2 id="preconditions">Preconditions</h2>
            <ul><li>the database exists</li></ul>
            """;

        AcceptanceCriteriaSection.Read(html).Should().Equal(
            ["the table exists", "the repository reads it & writes it"],
            "Azure DevOps stores the markdown body as HTML, and the section must end at the next heading");
    }
}
