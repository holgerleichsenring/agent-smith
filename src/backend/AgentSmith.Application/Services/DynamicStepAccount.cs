using System.Text;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services;

/// <summary>
/// 2026-08-30-26ed: names every dynamic scan step that ran and says whether it
/// contributed evidence, and when it did not, why. A step that found nothing is
/// otherwise absent from the findings summary, which reads the same whether the
/// target was clean or the step never got that far.
/// </summary>
internal static class DynamicStepAccount
{
    internal const string Heading = "### Dynamic step coverage";

    internal static void Append(StringBuilder sb, NucleiResult? nuclei, ZapResult? zap)
    {
        if (nuclei is null && zap is null)
            return;

        sb.AppendLine(Heading);
        if (nuclei is not null)
            sb.AppendLine(Line("Nuclei", nuclei.Findings.Count, nuclei.DegradedReason));
        if (zap is not null)
            sb.AppendLine(Line("ZAP", zap.Findings.Count, zap.DegradedReason));
        sb.AppendLine();
    }

    private static string Line(string step, int findings, string? degradedReason) =>
        (findings, degradedReason) switch
        {
            ( > 0, null) => $"- {step}: contributed {Evidence(findings)}.",
            ( > 0, _) => $"- {step}: contributed {Evidence(findings)}, but its coverage is partial — {degradedReason}.",
            (_, null) => $"- {step}: contributed nothing — it ran to its end and found nothing.",
            _ => $"- {step}: contributed nothing, and this is not evidence of a clean target — {degradedReason}.",
        };

    private static string Evidence(int findings) => findings == 1 ? "1 finding" : $"{findings} findings";
}
