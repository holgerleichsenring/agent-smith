using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// 2026-11-05-862ea: what a design turn is told about its sources. 2026-11-09-0620b: told what each source shows rather
/// than which to read first — a turn told to read every upload before the repositories never read the repositories, and
/// asked the operator questions the repositories already answered.
/// </summary>
public static class DialogReadingLine
{
    public const string CodeMap =
        "The code map above says where things live and how they relate. When an answer rests on what a file actually "
        + "says, read the file with your tools.";

    public const string Uploads =
        "Two sources are in front of you. The uploads show what the operator wants built. The repositories show what "
        + "already exists and what it already decides — how a caller is authenticated, how data is stored, how messages "
        + "travel. A proposal rests on both: the behaviour the uploads ask for, built the way the repositories already "
        + "build it wherever they have a counterpart. Under each upload the prompt says how much of it earlier turns "
        + "opened; note_reference keeps what an area says for later turns.";

    public static string Render(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        var holdsUpload = pipeline.TryGet<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes, out var sandboxes)
            && sandboxes is not null
            && sandboxes.Keys.Any(key => key.StartsWith(ReferenceScopeName.Prefix, StringComparison.Ordinal));
        return holdsUpload ? Uploads : CodeMap;
    }
}
