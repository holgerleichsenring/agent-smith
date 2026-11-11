using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// 2026-11-05-862ea: the work picture in a design turn's prompt. 2026-11-09-0620k: only what the server knows — whether a
/// picture is stored and its values. When to state one, what it holds and how it sets the reading are the skill's.
/// </summary>
public static class WorkPictureSection
{
    public const string NoneStored = "No work picture is stored yet.";

    public static string Render(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (!pipeline.TryGet<WorkPicture>(ContextKeys.SpecDialogWorkPicture, out var picture) || picture is null)
            return NoneStored;
        var lines = new List<string>
        {
            "## Work picture",
            $"- kind: {WorkPictureText.Dashed(picture.Kind.ToString())}",
            $"- truth: {picture.Truth}",
            $"- size: {WorkPictureText.Dashed(picture.Size.ToString())}",
        };
        // 2026-11-10-548f: the stated effort and the cap it sized for this turn, in place.
        if (picture.Effort != Contracts.Models.Configuration.ComplexityTier.Unknown)
            lines.Add($"- effort: {picture.Effort.ToString().ToLowerInvariant()}"
                + (pipeline.TryGet<Contracts.Models.Configuration.CostCapValues>("PipelineCostCap", out var cap) && cap is not null
                    ? $" (cost cap ${cap.Usd:0.##} / {cap.Tokens:N0} tokens)" : string.Empty));
        return string.Join("\n", lines);
    }
}
