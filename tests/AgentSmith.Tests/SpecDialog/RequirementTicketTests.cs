using AgentSmith.Application.Services.PhaseExecution;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Validation;
using AgentSmith.Contracts.Models;
using FluentAssertions;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-09-13-b7ba: an epic's tickets are REQUIREMENTS — what is wanted and why. A single
/// phase filed to be worked now keeps its work order.
/// </summary>
public sealed class RequirementTicketTests
{
    private const string Yaml = """
        phase: p9000a
        goal: Widget storage layer
        scope:
          in: >
            The table, the migration and the repository that reads it.
          out: >
            THE API ON TOP. Its own slice, and it needs this one first.
        decisions:
          - key: |
              A TABLE BEFORE AN API, BECAUSE THE API SHAPE FOLLOWS THE STORAGE.
        steps:
          - id: the-table
            action: "Add the table and its migration."
        done:
          - "the table exists and the repository reads it"
        """;

    private static readonly PhaseDraft Draft =
        new("p9000a", "Widget storage layer", Yaml, ["p9000"]);

    [Fact]
    public void RenderChildRequirement_Body_HasNoFencedBlock() =>
        Render().Should().NotContain("```");

    [Fact]
    public void RenderChildRequirement_Body_RendersScopeInAndOut()
    {
        var body = Render();

        body.Should().Contain("## Scope");
        body.Should().Contain("The table, the migration");
        body.Should().Contain("THE API ON TOP",
            "what a slice deliberately excludes is half of what a requirement says");
    }

    [Fact]
    public void RenderChildRequirement_Body_OmitsSteps() =>
        Render().Should().NotContain("Add the table and its migration",
            "carrying the cut in prose would let the deriver reproduce the cut it must redo");

    [Fact]
    public void RenderBody_MapShapedDecisions_AppearInTheReasoning() =>
        Render().Should().Contain("A TABLE BEFORE AN API",
            "every modern decision is a {key: '…'} map, which used to render as an empty "
            + "line and be filtered away — so no filed ticket has ever carried its reasoning");

    [Fact]
    public void RenderChildRequirement_Body_NamesItsRequires() =>
        Render().Should().Contain("## Requires").And.Contain("p9000");

    /// <summary>
    /// 2026-09-17-042ea: the tracker links the child to its parent and a label stamps it; a line in
    /// the body was a segment the deriver had to carry or discard, and nothing parsed it.
    /// </summary>
    [Fact]
    public void RequirementTicket_Body_CarriesNoParentLine() =>
        Render().Should().NotContain("Parent:");

    [Fact]
    public void RenderPhase_SinglePhaseOutcome_StillEmbedsTheSpec()
    {
        var body = new PhaseTicketRenderer().RenderPhase(Draft).Body;

        body.Should().Contain("```yaml").And.Contain("phase: p9000a");
        body.Should().Contain("Add the table and its migration",
            "a work order is cut against the repository as it is and filed to be worked now");
    }

    [Fact]
    public void SpecSource_RequirementTicket_LeavesTheDerivationToRun()
    {
        var extraction = new PhaseSpecFromTicket(
            new SpecDraftValidator(new PhaseSpecSchemaProvider()), new PhaseDraftReader())
            .Extract(Render());

        extraction.Should().BeOfType<PhaseSpecInvalid>()
            .Which.IsAbsent.Should().BeTrue(
                "absent — not malformed — is what makes SpecSourceResolver fall through to "
                + "Derived instead of failing the run");
    }

    private static string Render() =>
        new PhaseTicketRenderer().RenderChildRequirement(Draft).Body;
}
