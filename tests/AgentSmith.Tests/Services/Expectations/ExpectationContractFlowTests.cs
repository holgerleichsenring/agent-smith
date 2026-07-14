using AgentSmith.Application.Prompts;
using AgentSmith.Application.Services.Expectations;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Prompts;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Expectations;
using FluentAssertions;

namespace AgentSmith.Tests.Services.Expectations;

/// <summary>p0328: the ratified expectation flows into the plan prompt
/// ({ExpectationSection}) and the PR body (assertions as checkboxes).</summary>
public sealed class ExpectationContractFlowTests
{
    private static readonly ExpectationDraft Draft = new(
        "The endpoint returns 500 on empty payloads.",
        ["The endpoint returns 400 on empty payloads.", "Existing callers stay unaffected."],
        ["No new dependencies."],
        null);

    [Fact]
    public void GeneratePlan_Prompt_ContainsRatifiedExpectation()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.RunExpectation, Ratified(ExpectationOutcomes.Verbatim));
        var builder = new AgentPromptBuilder(Catalog());

        var prompt = builder.BuildPlanSystemPrompt(
            "principles", codeMap: null, projectContext: null,
            ExpectationPromptSection.Build(pipeline));

        prompt.Should().Contain("Acceptance contract");
        prompt.Should().Contain("The endpoint returns 400 on empty payloads.");
        prompt.Should().Contain("Existing callers stay unaffected.");
        prompt.Should().NotContain("{ExpectationSection}", "the token must be bound");
    }

    [Fact]
    public void GeneratePlan_Prompt_NoExpectation_OmitsSectionWithoutUnboundToken()
    {
        var builder = new AgentPromptBuilder(Catalog());

        var prompt = builder.BuildPlanSystemPrompt("principles", codeMap: null);

        prompt.Should().NotContain("Acceptance contract");
        prompt.Should().NotContain("{ExpectationSection}");
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

    private static EmbeddedPromptCatalog Catalog() => new(
        new EnvDirectoryPromptOverrideSource(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<EnvDirectoryPromptOverrideSource>.Instance),
        Microsoft.Extensions.Logging.Abstractions.NullLogger<EmbeddedPromptCatalog>.Instance);

    private static RatifiedExpectation Ratified(string outcome) => new(
        Draft, outcome, "@operator", DateTimeOffset.UtcNow,
        outcome == ExpectationOutcomes.Edited ? 7 : 0);
}
