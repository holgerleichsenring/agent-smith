using AgentSmith.Application.Services.Rework;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Runs;

namespace AgentSmith.Application.Services.Prompts;

/// <summary>
/// 2026-10-13-8762p: the defaults a person overruled on the ticket page since the previous attempt read its acts
/// — each question, the answer taken by default and the person's note — as a delimited prompt section, untrusted
/// data like the review it follows.
/// </summary>
public static class OverruledDefaultsPromptSection
{
    public const string Heading = "## Overruled defaults";

    public static string Render(PipelineContext pipeline) =>
        OverruleAct.TextOf(pipeline.TryGet<PreviousAttempt>(ContextKeys.PreviousAttempt, out var attempt) ? attempt : null) is { } text
            ? TicketPromptDelimiters.WrapSection(Heading, text)
            : string.Empty;
}
