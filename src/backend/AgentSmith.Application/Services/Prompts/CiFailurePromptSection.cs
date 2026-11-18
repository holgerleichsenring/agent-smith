using AgentSmith.Application.Services.Rework;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Reviews;

namespace AgentSmith.Application.Services.Prompts;

/// <summary>
/// 2026-11-17-3435c: the red CI a fix run serves, as a delimited prompt section — untrusted data like the conversation:
/// per pull request and failed check its link and the bounded excerpt the record stored. The task is to make it green
/// on the same pull request.
/// </summary>
public static class CiFailurePromptSection
{
    public const string Heading = "## CI on the pull request is red";

    public static string Render(PipelineContext pipeline)
    {
        if (!pipeline.TryGet<IReadOnlyList<CiFailureFeedback>>(ContextKeys.CiFailures, out var red) || red is not { Count: > 0 })
            return string.Empty;
        return TicketPromptDelimiters.WrapSection(Heading,
            "The host's CI failed on the head this ticket's last run pushed. Find the cause, fix it, verify it locally, and the "
            + "run pushes to the same pull request; CI is watched again.\n\n" + CiEvidenceText.Of(red));
    }
}
