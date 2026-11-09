using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// 2026-11-05-862ea: the work picture in a design turn's prompt. With none stored, the turn is asked to state one in
/// the first line of its answer to a request for work; with one stored, its values are rendered with what its kind
/// reads. 2026-11-09-0620b: the repositories are what already exists, not scaffolding, and readiness is the model's
/// call, so no line keys it to a phrase.
/// </summary>
public static class WorkPictureSection
{
    public const string NoneStored =
        "No work picture is stored yet. When the operator asks for work, the first line of your answer states it — the "
        + "kind of undertaking, where what is wanted is described (an upload, the ticket, the conversation) and what the "
        + "repositories already provide, the expected size — and you record it with " + WorkPictureToolHost.Name
        + ". A question, or a request for a text to take elsewhere, needs none.";

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
        return string.Join("\n", lines);
    }

    private static string? Reading(UndertakingKind kind) => kind switch
    {
        UndertakingKind.Rebuild =>
            "A rebuild carries over everything the source does, and lands in repositories whose existing decisions it keeps.",
        UndertakingKind.Feature => "A feature touches the code it changes and every upload the picture names.",
        UndertakingKind.Fix => "A fix touches the code it changes.",
        _ => null,
    };
}
