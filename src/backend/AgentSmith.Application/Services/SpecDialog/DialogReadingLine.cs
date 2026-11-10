using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// 2026-11-05-862ea: what a design turn was told about its sources. 2026-11-09-0620k: nothing — the design-partner skill
/// (v5.25.0 on) says what uploads and repositories each show and how to orient without a code map, so a server line
/// would repeat it. The turn's uploads and their read coverage are listed in their own section.
/// </summary>
public static class DialogReadingLine
{
    public static string Render(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        return string.Empty;
    }
}
