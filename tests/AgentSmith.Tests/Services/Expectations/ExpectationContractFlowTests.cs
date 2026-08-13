using AgentSmith.Application.Services.Expectations;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Expectations;
using FluentAssertions;

namespace AgentSmith.Tests.Services.Expectations;

/// <summary>p0328: the ratified expectation flows into the master prompt
/// ({ExpectationSection}) and the PR body (assertions as checkboxes).</summary>
public sealed class ExpectationContractFlowTests
{
    private static readonly ExpectationDraft Draft = new(
        "The endpoint returns 500 on empty payloads.",
        ["The endpoint returns 400 on empty payloads.", "Existing callers stay unaffected."],
        ["No new dependencies."],
        null);

    [Fact]
    public void ExpectationSection_ContainsRatifiedExpectation()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.RunExpectation, Ratified(ExpectationOutcomes.Verbatim));

        var section = ExpectationPromptSection.Build(pipeline);

        section.Should().Contain("Acceptance contract");
        section.Should().Contain("The endpoint returns 400 on empty payloads.");
        section.Should().Contain("Existing callers stay unaffected.");
    }

    [Fact]
    public void ExpectationSection_NoExpectation_RendersNothing()
    {
        ExpectationPromptSection.Build(new PipelineContext()).Should().BeEmpty();
    }

    [Fact]
    public void PrBody_RendersAssertionsAsChecklist()
    {
        var body = ExpectationPrBodySection.Build(Ratified(ExpectationOutcomes.Edited));

        body.Should().Contain("## Acceptance contract (ratified edited by @operator)");
        body.Should().Contain("- [ ] The endpoint returns 400 on empty payloads.");
        body.Should().Contain("- [ ] Existing callers stay unaffected.");
        body.Should().Contain("**Constraints:**");
        body.Should().Contain("- No new dependencies.");
    }

    [Fact]
    public void PrBody_UnratifiedExpectation_StampsVisibleDegradation()
    {
        var body = ExpectationPrBodySection.Build(Ratified(ExpectationOutcomes.Unratified));

        body.Should().Contain("unratified — auto-ratified headless, no human review");
    }

    [Fact]
    public void PrBody_NoExpectation_RendersNothing()
    {
        ExpectationPrBodySection.Build(new PipelineContext()).Should().BeEmpty();
    }

    private static RatifiedExpectation Ratified(string outcome) => new(
        Draft, outcome, "@operator", DateTimeOffset.UtcNow,
        outcome == ExpectationOutcomes.Edited ? 7 : 0);
}
