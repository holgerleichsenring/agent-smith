using System.Text;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Expectations;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0328: renders the ratified expectation into the PR body — assertions as
/// checkboxes so the reviewer checks the change against the ratified contract
/// (later evidence phases check them off). Headless auto-ratification stamps
/// the heading 'unratified' — visible degradation. Empty when the run
/// negotiated nothing. Pure mapping.
/// </summary>
internal static class ExpectationPrBodySection
{
    public static string Build(PipelineContext pipeline) =>
        pipeline.TryGet<RatifiedExpectation>(ContextKeys.RunExpectation, out var expectation)
        && expectation is not null
            ? Build(expectation)
            : string.Empty;

    internal static string Build(RatifiedExpectation expectation)
    {
        var stamp = expectation.IsUnratified
            ? " (unratified — auto-ratified headless, no human review)"
            : $" (ratified {expectation.Outcome} by {expectation.RatifiedBy})";
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine($"## Acceptance contract{stamp}");
        sb.AppendLine();
        foreach (var assertion in expectation.Draft.Expected)
            sb.AppendLine($"- [ ] {assertion}");
        AppendConstraints(sb, expectation);
        return sb.ToString().TrimEnd('\n');
    }

    private static void AppendConstraints(StringBuilder sb, RatifiedExpectation expectation)
    {
        if (expectation.Draft.Constraints.Count == 0) return;
        sb.AppendLine();
        sb.AppendLine("**Constraints:**");
        foreach (var constraint in expectation.Draft.Constraints)
            sb.AppendLine($"- {constraint}");
    }
}
