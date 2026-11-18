using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>2026-11-17-3435c: the ticket comment of a CI-fix hand-back — the reason carries the evidence.</summary>
public static class CiFixBudgetComment
{
    public const string Heading = "## Agent Smith — the CI on the pull request is still red";

    public static string Build(SpecHandback handback)
    {
        ArgumentNullException.ThrowIfNull(handback);
        return $"{Heading}\n\n{handback.Reason}\n\nNothing more is tried on its own. Reply with what changes the picture, "
            + "or fix the pipeline and push; a comment or an approval starts the next attempt and the count starts again.";
    }
}
