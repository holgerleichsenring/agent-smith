using System.Text.Json;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Validation;
using AgentSmith.Contracts.Models;
using AgentSmith.Infrastructure.Services.Providers.Tickets;
using AgentSmith.Tests.Architecture;
using FluentAssertions;
using Markdig;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-10-01-f5c3b: a filed phase or epic ticket reads goal, Why, What changes, Acceptance
/// criteria, Out of scope, Preconditions — and its criteria read back as PhaseDraft.Done from
/// every encoding a tracker hands a body back in. A bug's criteria are list items too.
/// </summary>
public sealed class TicketReaderShapeTests
{
    private const string Yaml = """
        phase: p9000a
        goal: Widgets are stored and read back
        requires: [p8999]
        scope:
          in: The table, the migration and the repository that reads it.
          out: The API on top of it.
        decisions:
          - key: "A TABLE FIRST, because the API shape follows the storage."
        steps:
          - id: the-table
            action: "Add the table and its migration."
        done:
          - "the table exists"
          - given: a stored widget
            when: the repository reads it back
            then: the same widget is returned
        """;

    private static readonly PhaseDraft Draft = new PhaseDraftReader().Read(Yaml);

    [Fact]
    public void RequirementBody_LeadsWithTheGoalThenWhyWhatChangesCriteriaOutOfScope()
    {
        var body = new PhaseTicketRenderer().RenderPhase(Draft).Body;

        InOrder(body, "Widgets are stored and read back", "## Why", "## What changes",
            AcceptanceCriteriaSection.Heading, "## Out of scope", "## Preconditions");
        body.Should().NotContain("## Goal").And.NotContain("## Scope").And.NotContain("Add the table");
    }

    [Fact]
    public void RenderEpicParent_LeadsWithTheGoal_ThenTheReaderSections_ThenSlices()
    {
        var body = new PhaseTicketRenderer().RenderEpicParent(Draft, [Draft]).Body;

        InOrder(body, "Widgets are stored and read back", "## Why", "## What changes",
            AcceptanceCriteriaSection.Heading, "## Out of scope", "## Preconditions", "## Slices");
    }

    [Fact]
    public void RequirementBody_ScenarioCriterion_ReadsBackAsPhaseDraftDone_Markdown() =>
        AcceptanceCriteriaSection.Read(Body()).Should().Equal(Draft.Done);

    [Fact]
    public void RequirementBody_ScenarioCriterion_ReadsBackAsPhaseDraftDone_AzureDevOpsHtml()
    {
        var html = Markdown.ToHtml(Body(), new MarkdownPipelineBuilder().UseAdvancedExtensions().Build());

        AcceptanceCriteriaSection.Read(html).Should().Equal(Draft.Done);
    }

    [Fact]
    public void RequirementBody_ScenarioCriterion_ReadsBackAsPhaseDraftDone_JiraParagraphs()
    {
        using var document = JsonSerializer.SerializeToDocument(JiraAdfRenderer.FromMultilineText(Body()));

        AcceptanceCriteriaSection.Read(JiraAdfParser.ExtractText(document.RootElement)).Should().Equal(Draft.Done);
    }

    [Fact]
    public void Parse_BugAcceptanceCriteriaAsAList_KeepsEveryItem()
    {
        var map = OutcomeYamlReader.ReadMap(
            "kind: bug\ntitle: t\ndescription: It drops.\nacceptance_criteria:\n  - it stops dropping\n  - the log is quiet\n");

        var bug = new BugOutcomeParser().Parse(map)
            .Should().BeOfType<OutcomeResolved>().Which.Proposal.Should().BeOfType<BugOutcome>().Subject;

        AcceptanceCriteriaSection.Read(new BugTicketRenderer().RenderBody(bug.Ticket))
            .Should().Equal("it stops dropping", "the log is quiet");
    }

    [Fact]
    public void RenderBody_UnlistedAcceptanceCriteriaLines_BecomeOneItemEach()
    {
        var body = new BugTicketRenderer().RenderBody(new BugTicketDraft(
            "t", "It drops.", "It stops dropping.\n- the log is quiet\n  even under load\n\nRetries   succeed."));

        AcceptanceCriteriaSection.Read(body).Should().Equal(
            "It stops dropping.", "the log is quiet even under load", "Retries succeed.");
    }

    [Fact]
    public void TicketTemplateDoc_AcceptanceCriteria_ReadAsCriteria()
    {
        var page = File.ReadAllText(Path.Combine(
            ArchitectureSources.RepositoryRoot, "docs", "trigger-it", "writing-a-ticket.md"));
        var start = page.IndexOf("```markdown\n", StringComparison.Ordinal) + "```markdown\n".Length;
        var template = page[start..page.IndexOf("\n```", start, StringComparison.Ordinal)];

        AcceptanceCriteriaSection.Read(template).Should().HaveCount(2)
            .And.Contain(c => c.StartsWith("GIVEN ", StringComparison.Ordinal) && c.Contains(" THEN "));
    }

    [Fact]
    public void Validate_TheSampleSpec_IsSchemaValid() =>
        new SpecDraftValidator(new PhaseSpecSchemaProvider()).ValidateYaml(Yaml).Should().BeOfType<SpecDraftValid>();

    private static string Body() => new PhaseTicketRenderer().RenderPhase(Draft).Body;

    private static void InOrder(string body, params string[] parts)
    {
        var positions = parts.Select(part => body.IndexOf(part, StringComparison.Ordinal)).ToList();
        positions.Should().NotContain(-1, "every section is present");
        positions.Should().BeInAscendingOrder("the sections read in the reader's order");
    }
}
