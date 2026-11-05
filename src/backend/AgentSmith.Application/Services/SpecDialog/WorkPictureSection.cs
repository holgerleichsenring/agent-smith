using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// 2026-11-05-862ea: the work picture in a design turn's prompt. With none stored, the turn is asked to state one in
/// the first line of its answer to a request for work; with one stored, its values are rendered with the reading line
/// its kind selects and the readiness line its size selects.
/// </summary>
public static class WorkPictureSection
{
    public const string NoneStored =
        "No work picture is stored yet. When the operator asks for work, the first line of your answer states it — the "
        + "kind of undertaking, where the truth lives (which upload, which repositories are scaffolding), the expected "
        + "size — and you record it with " + WorkPictureToolHost.Name + ". A question, or a request for a text to take "
        + "elsewhere, needs none.";

    public const string Correction =
        "The operator corrects it in a sentence; record a correction with " + WorkPictureToolHost.Name + ".";

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
            Correction,
        };
        if (Reading(picture.Kind) is { } reading) lines.Add(reading);
        lines.Add(Readiness(picture.Size));
        return string.Join("\n", lines);
    }

    private static string? Reading(UndertakingKind kind) => kind switch
    {
        UndertakingKind.Rebuild => "Read the truth in full before you say you are ready; the repositories are compared against it.",
        UndertakingKind.Feature => "Read the code the feature touches and every upload the truth names before you say you are ready.",
        UndertakingKind.Fix => "Read the code the fix touches.",
        _ => null,
    };

    private static string Readiness(UndertakingSize size) => size == UndertakingSize.OneSpec
        ? "You are ready once the decisions you asked are answered."
        : "You are ready once the truth is read and the cut is agreed with the operator in outline.";
}
