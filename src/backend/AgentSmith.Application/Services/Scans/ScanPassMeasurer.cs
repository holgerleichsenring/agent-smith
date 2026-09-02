using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Surface;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Scans;

/// <summary>
/// 2026-09-01-3653: reads off the run how large the scan's prompt was, how many turns the
/// pass used against its ceiling, and how much of the source it read.
/// <para>
/// The named sections are re-rendered from the same pipeline the prompt factory reads, so
/// the sizes are the sizes that were sent — no plumbing through the factory, and no second
/// copy of the composition to keep in step. Four sections, not twelve placeholders: the
/// question is whether the scan prompt is fifty-two thousand characters or twelve, and the
/// system prompt's total plus the review prompt's composed parts answers it.
/// </para>
/// </summary>
public static class ScanPassMeasurer
{
    /// <summary>The measures, or null when no master pass ran — a run with no master OMITS
    /// the section rather than reporting a row of zeroes that reads like a measurement.</summary>
    public static ScanPassMeasures? Measure(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (!pipeline.TryGet<int>(ContextKeys.MasterSystemPromptChars, out var systemPrompt))
            return null;

        pipeline.TryGet<int>(ContextKeys.MasterTurnsUsed, out var turns);
        pipeline.TryGet<int>(ContextKeys.ScanMasterIterationCeiling, out var ceiling);
        return new ScanPassMeasures(
            SystemPromptChars: systemPrompt,
            ConversationChars: ConversationChars(pipeline),
            ScannerFindingsChars: ScanFindingsSection.Render(pipeline).Length,
            OpenApiDocumentChars: OpenApiChars(pipeline),
            SurfaceDifferenceChars: SurfaceDifferencePromptSection.Render(pipeline).Length,
            TurnsUsed: turns,
            IterationCeiling: ceiling,
            DistinctReadCount: DistinctReads(pipeline));
    }

    private static int ConversationChars(PipelineContext pipeline) =>
        pipeline.TryGet<IReadOnlyList<TicketComment>>(ContextKeys.TicketComments, out var comments)
            ? Prompts.TicketConversationPromptSection.Render(comments).Length
            : 0;

    private static int OpenApiChars(PipelineContext pipeline) =>
        pipeline.TryGet<SwaggerSpec>(ContextKeys.SwaggerSpec, out var spec) && spec?.RawJson is { } json
            ? json.Length
            : 0;

    /// <summary>p0279 already publishes the read-set; this counts it rather than rebuilding it.</summary>
    private static int DistinctReads(PipelineContext pipeline) =>
        pipeline.TryGet<List<string>>(ContextKeys.MasterReadPaths, out var paths) && paths is not null
            ? paths.Distinct(StringComparer.Ordinal).Count()
            : 0;
}
