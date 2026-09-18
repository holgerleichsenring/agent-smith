using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Validation;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-09-17-042eb: the section is read out of tickets people write too. Only list items are
/// criteria, exactly one marker comes off each, and what a form or a template leaves behind —
/// placeholders, comments, code — is never a criterion the run is held to.
/// </summary>
public sealed class AcceptanceCriteriaSectionTests
{
    [Fact]
    public void AcceptanceCriteriaSection_GitHubIssueFormPlaceholder_IsNoCriterion() =>
        AcceptanceCriteriaSection.Read("### Acceptance Criteria\r\n\r\n_No response_\r\n\r\n### Notes\r\n\r\n- not a criterion")
            .Should().BeEmpty("an empty issue-form field is a placeholder, and the next heading ends the section");

    [Fact]
    public void AcceptanceCriteriaSection_HtmlCommentAndProse_AreNoCriteria() =>
        AcceptanceCriteriaSection.Read("## Acceptance criteria\n<!-- list what must be true -->\nSome prose.\n- the table exists\n")
            .Should().Equal("the table exists");

    [Fact]
    public void AcceptanceCriteriaSection_FencedBlock_NeitherEndsTheSectionNorCounts() =>
        AcceptanceCriteriaSection.Read("## Acceptance criteria\n- first\n```bash\n# a comment, not a heading\n- not an item\n```\n- second\n## Next\n- outside")
            .Should().Equal("first", "second");

    [Fact]
    public void AcceptanceCriteriaSection_StripsExactlyOneMarker() =>
        AcceptanceCriteriaSection.Read("## Acceptance criteria\n- --dry-run exits 0\n- **the table** exists\n- [ ] the box is ticked\n* [x] done already\n1. numbered first\n2) numbered second\n")
            .Should().Equal("--dry-run exits 0", "**the table** exists", "the box is ticked", "done already", "numbered first", "numbered second");

    [Fact]
    public void AcceptanceCriteriaSection_NestedAndWrappedLines_BelongToTheirItem() =>
        AcceptanceCriteriaSection.Read("## Acceptance criteria\n- the API returns a widget\n  - with its id\n  and its name\n- the store is empty after a delete\n")
            .Should().Equal("the API returns a widget with its id and its name", "the store is empty after a delete");

    [Fact]
    public void SpecFallback_FieldCriteria_KeepLeadingDashesInsideTheText()
    {
        var ticket = new Ticket(new TicketId("42"), "Widget", "body", "- --dry-run exits 0\n- **the table** exists\n_No response_", "open", "test");
        var validator = new SpecDraftValidator(new PhaseSpecSchemaProvider());
        var fallback = new SpecFallback(validator, new PhaseDraftReader(), new DerivedPhaseYamlRenderer());

        var set = fallback.Build("key", ticket, TicketSegmenter.Segment("body"), [], SpecSource.Derived);

        set.Phases.Single().Draft.Done.Should().Equal("--dry-run exits 0", "**the table** exists");
    }
}
