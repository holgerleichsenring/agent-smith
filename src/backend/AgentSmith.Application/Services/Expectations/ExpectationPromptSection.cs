using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Expectations;

namespace AgentSmith.Application.Services.Expectations;

/// <summary>
/// p0328: renders the {ExpectationSection} prompt token — the ratified
/// expectation as the BINDING acceptance contract for planner and master.
/// Empty when the run negotiated nothing (other presets, ticketless runs);
/// a master body without the placeholder simply never renders it (old skills
/// pins keep working — Render's token replace is a no-op then).
/// </summary>
public static class ExpectationPromptSection
{
    public static string Build(PipelineContext pipeline) =>
        pipeline.TryGet<RatifiedExpectation>(ContextKeys.RunExpectation, out var expectation)
        && expectation is not null
            ? Build(expectation)
            : string.Empty;

    public static string Build(RatifiedExpectation expectation)
    {
        var stamp = expectation.IsUnratified
            ? " (UNRATIFIED — auto-ratified without human review)"
            : string.Empty;
        return $"""
            ## Acceptance contract{stamp}
            The ratified expectation below is the binding acceptance contract for this run.
            Implement exactly what it asserts — no more, no less. Every "Expected" assertion
            must hold after your change; every constraint must be respected.

            {ExpectationMarkdown.Render(expectation.Draft)}

            """;
    }
}
