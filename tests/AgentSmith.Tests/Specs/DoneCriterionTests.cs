using AgentSmith.Application.Services;
using AgentSmith.Application.Services.PhaseExecution;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Validation;
using AgentSmith.Contracts.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-10-01-f5c3a: a done criterion may be a when/then scenario besides one line, and every
/// reader of the done list sees one plain line either way — the reader, the hand-written
/// ticket, the epic child, the execution prompt and the filed ticket.
/// </summary>
public sealed class DoneCriterionTests
{
    private const string Mixed = """
        spec: p9000a
        goal: Widget storage layer
        done:
          - "  the table exists  "
          - given: a stored widget
            when: |
              the repository reads
              it back
            then: the same widget is returned
          - when: the table is empty
            then: the read returns nothing
        """;

    private const string GivenWhenThen =
        "GIVEN a stored widget WHEN the repository reads it back THEN the same widget is returned";

    private static readonly SpecDraftValidator Validator = new(new PhaseSpecSchemaProvider());

    [Fact]
    public void Line_AScenarioWithGiven_IsGivenWhenThenInPlainKeywords() =>
        DoneCriterion.Line(Scenario(("given", "a stored widget"),
                ("when", "the repository reads\n  it back"), ("then", "the same widget is returned")))
            .Should().Be(GivenWhenThen);

    [Fact]
    public void Line_AScenarioWithoutGiven_StartsWithWhen() =>
        DoneCriterion.Line(Scenario(("when", "the table is empty"), ("then", "the read returns nothing")))
            .Should().Be("WHEN the table is empty THEN the read returns nothing");

    [Fact]
    public void Line_AString_IsTrimmed() =>
        DoneCriterion.Line("  the table exists \n").Should().Be("the table exists");

    [Fact]
    public void Lines_ADoneList_GivesOneLinePerEntry() =>
        DoneCriterion.Lines(new Dictionary<string, object?> { ["done"] = new List<object?> { "a", "b" } })
            .Should().Equal("a", "b");

    [Fact]
    public void Validate_ADoneScenarioWithAnUnknownKey_IsRefused() =>
        Validator.ValidateYaml("spec: p9000a\ngoal: g\ndone:\n  - when: w\n    then: t\n    because: b\n")
            .Should().BeOfType<SpecDraftInvalid>();

    [Fact]
    public void Read_ADoneListMixingLinesAndScenarios_GivesOneLineEach()
    {
        Validator.ValidateYaml(Mixed).Should().BeOfType<SpecDraftValid>();

        new PhaseDraftReader().Read(Mixed).Done.Should().Equal(
            "the table exists", GivenWhenThen, "WHEN the table is empty THEN the read returns nothing");
    }

    [Fact]
    public void Extract_AHandWrittenTicketWithAScenario_CarriesItsLine() =>
        new PhaseSpecFromTicket(Validator, new PhaseDraftReader())
            .Extract($"A widget phase.\n\n```yaml\n{Mixed}\n```\n")
            .Should().BeOfType<PhaseSpecExtracted>()
            .Which.Draft.Done.Should().Contain(GivenWhenThen);

    [Fact]
    public void Parse_AnEpicChildWithOnlyScenarios_HasDone()
    {
        var parser = new EpicOutcomeParser(Validator, new PhaseDraftReader(), new RequiresEdgeChecker());
        var yaml = """
            kind: epic
            parent: { spec: p9000, goal: Widget platform }
            children:
              - spec: p9000a
                goal: Widget storage layer
                done: [{ when: a widget is saved, then: it is stored }]
              - spec: p9000b
                goal: Widget API
                done: [{ when: the API is called, then: a stored widget returns }]
            """;

        var epic = parser.Parse(OutcomeYamlReader.ReadMap(yaml))
            .Should().BeOfType<OutcomeResolved>().Which.Proposal.Should().BeOfType<EpicOutcome>().Subject;

        epic.Children[0].Done.Should().Equal("WHEN a widget is saved THEN it is stored");
    }

    [Fact]
    public void DoneCriteria_AScenario_RendersItsLineNotATypeName() =>
        PhaseExecutionPromptBlocks.DoneCriteria(new PhaseDraftReader().Read(Mixed))
            .Should().Contain("- " + GivenWhenThen).And.NotContain("Dictionary");

    [Fact]
    public void RequirementBody_AScenarioCriterion_ListsItsLineNotATypeName()
    {
        var body = new PhaseTicketRenderer().RenderPhase(new PhaseDraftReader().Read(Mixed)).Body;

        body.Should().Contain("- " + GivenWhenThen).And.NotContain("Dictionary");
        AcceptanceCriteriaSection.Read(body).Should().Contain(GivenWhenThen);
    }

    private static Dictionary<object, object?> Scenario(params (string Key, string Text)[] parts) =>
        parts.ToDictionary(p => (object)p.Key, p => (object?)p.Text);
}
