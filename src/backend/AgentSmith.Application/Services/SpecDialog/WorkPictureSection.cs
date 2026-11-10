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
        return string.Join("\n",
            "## Work picture",
            $"- kind: {WorkPictureText.Dashed(picture.Kind.ToString())}",
            $"- truth: {picture.Truth}",
            $"- size: {WorkPictureText.Dashed(picture.Size.ToString())}");
    }
}
