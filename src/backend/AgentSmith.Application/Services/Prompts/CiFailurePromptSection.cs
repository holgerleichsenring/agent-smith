using AgentSmith.Application.Services.Rework;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Reviews;

namespace AgentSmith.Application.Services.Prompts;

/// <summary>
/// 2026-11-17-3435c, recovered by 2026-11-26-49d3a: the red CI on the ticket's open pull requests, as a delimited prompt
/// section — untrusted data like the conversation: per pull request and failed check its link and the bounded excerpt the
/// record stored (failed tests with message and stack, or the failed step's log tail).
/// </summary>
public static class CiFailurePromptSection
{
    public const string Heading = "## CI on the pull request is red";

    public static string Render(PipelineContext pipeline)
    {
        if (!pipeline.TryGet<IReadOnlyList<CiFailureFeedback>>(ContextKeys.CiFailures, out var red) || red is not { Count: > 0 })
            return string.Empty;
        return TicketPromptDelimiters.WrapSection(Heading,
            "The host's CI failed on the head of this ticket's open pull request; what it reported:\n\n" + CiEvidenceText.Of(red));
    }
}
