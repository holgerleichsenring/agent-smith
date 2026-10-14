using System.Text;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-10-13-3890a: the one pass a surrendering master is given — each criterion not Met with the
/// reason the master itself stated, and what counts as work rather than a reason to stop.
/// </summary>
internal static class MasterChallengeNudge
{
    internal const string WorkNotReasons =
        "A command the repository's ecosystem ships as an installable tool (a dotnet tool, an npm dev "
        + "dependency, a pip package in a venv) that is missing is work: install it with run_command and "
        + "continue. A red test is work. A test the criteria ask for and nobody wrote is work. None of "
        + "these is a reason for not_applicable or blocked.";

    public static string Build(
        IReadOnlyList<CriterionPairing> pairings, MasterVerification? verification, string? blocker,
        string originalPrompt)
    {
        var sb = new StringBuilder();
        sb.Append("You are about to stop with ratified criteria not met and budget left. Before you do, ")
            .Append("look at your own reasons once more — if work in this repository can make a criterion ")
            .Append("true, do that work now, then give your verdict again.\n\n");
        if (verification?.Status == VerificationStatus.Failed) sb.Append("Your verdict is RED.\n");
        if (!string.IsNullOrWhiteSpace(blocker)) sb.Append("Your stated blocker: ").Append(blocker).Append('\n');
        sb.Append("\nCriteria not met:\n");
        foreach (var p in pairings.Where(p => p.Disposition?.Status != AcceptanceStatus.Met))
            sb.Append("- ").Append(p.Criterion).Append(" — ").Append(Reason(p)).Append('\n');
        sb.Append('\n').Append(WorkNotReasons).Append("\n\nOriginal task:\n").Append(originalPrompt);
        return sb.ToString();
    }

    private static string Reason(CriterionPairing pairing) => pairing.Disposition switch
    {
        null => "no disposition given",
        { Status: AcceptanceStatus.NotApplicable } d => $"you declined it: {d.Evidence}",
        { } d => $"{d.Status}: {d.Evidence}",
    };
}
