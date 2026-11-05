using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// 2026-11-05-862ea: what a design turn reads first. A turn whose sandbox map holds an upload is told the uploads are
/// the subject, read before the repositories; a turn with none keeps the line that answers from the code map.
/// </summary>
public static class DialogReadingLine
{
    public const string CodeMap =
        "Answer from the code map above when it suffices; read source files through your tools only when the "
        + "question needs real file content.";

    public const string UploadsFirst =
        "The uploads are the subject of this conversation: read them before the repositories — every file read_file "
        + "can open, across turns, until the coverage under each upload says all are opened, unless the operator names "
        + "the part that matters. Record what each area says with note_reference, then compare the repositories against them.";

    public static string Render(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        var holdsUpload = pipeline.TryGet<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes, out var sandboxes)
            && sandboxes is not null
            && sandboxes.Keys.Any(key => key.StartsWith(ReferenceScopeName.Prefix, StringComparison.Ordinal));
        return holdsUpload ? UploadsFirst : CodeMap;
    }
}
