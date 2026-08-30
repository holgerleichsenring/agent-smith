using System.Text;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Surface;

/// <summary>
/// 2026-08-30-c6ec: how the difference reaches the reviewer — as evidence with its bounds
/// attached, never as a list of findings.
/// <para>
/// The section states what the reading covered before it states what it found, because the
/// reader has to be able to discount an entry: while a client file went undecided, an
/// operation shown here as unexercised may simply be one nobody could read the call to.
/// Each entry carries the requirement id that decides whether it matters.
/// </para>
/// </summary>
public static class SurfaceDifferencePromptSection
{
    public static string Render(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (!pipeline.TryGet<SurfaceDifferenceReport>(ContextKeys.SurfaceDifference, out var report)
            || report is null)
            return string.Empty;

        var builder = new StringBuilder("## Interface surface vs. its first-party clients\n\n");
        if (!report.Computed)
            return builder.Append($"Not computed — {report.NotComputedReason}.\n").ToString();

        builder.AppendLine(Bounds(report)).AppendLine();
        if (report.Differences.Count == 0)
            return builder.Append("No capability was found that the clients read here do not exercise.\n").ToString();

        foreach (var difference in report.Differences) builder.AppendLine(Line(difference));
        return builder.ToString();
    }

    private static string Bounds(SurfaceDifferenceReport report) =>
        $"What the clients exercise is a LOWER estimate, read from {report.Account.FilesRead.Count} "
        + $"file(s) with {report.Account.CallSitesFound} call site(s) found and "
        + $"{report.Account.FilesNotDecided.Count} file(s) that could not be decided"
        + (report.Degraded
            ? " — every entry below may be an artefact of one of those, so treat it as a question, not a fact."
            : ". Each entry is an OBSERVATION: the requirement named with it decides whether it matters.")
        + $" Requirement ids are of catalogue version {report.CatalogueVersion}.";

    private static string Line(SurfaceDifference difference) => difference.Kind switch
    {
        SurfaceDifferenceKind.UnexercisedOperation =>
            $"- {difference.Operation}: no client call site found [{difference.RequirementId}]",
        SurfaceDifferenceKind.UnsentAcceptedProperty =>
            $"- {difference.Operation}: accepts '{difference.Property}', no client sends it [{difference.RequirementId}]",
        _ =>
            $"- {difference.Operation}: returns '{difference.Property}', no client reads it [{difference.RequirementId}]",
    };
}
