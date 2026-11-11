using System.Globalization;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// 2026-11-05-862ea: the work picture in a design turn's prompt. 2026-11-09-0620k: only what the server knows — whether a
/// picture is stored and its values, and (205fc) the turn's cost cap. When to state one, what it holds and how it sets the reading are the skill's.
/// </summary>
public static class WorkPictureSection
{
    public const string NoneStored = "No work picture is stored yet.";

    public static string Render(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        // 2026-11-11-205fc: the cap this turn starts with, on every turn, so the effort is sized against a number.
        var cap = pipeline.TryGet<CostCapValues>("PipelineCostCap", out var c) && c is not null
            ? string.Create(CultureInfo.InvariantCulture, $"\nThis turn's cost cap: ${c.Usd:0.##} / {c.Tokens:N0} tokens.")
            : string.Empty;
        if (!pipeline.TryGet<WorkPicture>(ContextKeys.SpecDialogWorkPicture, out var picture) || picture is null)
            return NoneStored + cap;
        var lines = new List<string>
        {
            "## Work picture",
            $"- kind: {WorkPictureText.Dashed(picture.Kind.ToString())}",
            $"- truth: {picture.Truth}",
            $"- size: {WorkPictureText.Dashed(picture.Size.ToString())}",
        };
        if (picture.Effort != ComplexityTier.Unknown) // 2026-11-10-548f
            lines.Add($"- effort: {picture.Effort.ToString().ToLowerInvariant()}");
        return string.Join("\n", lines) + cap;
    }
}
