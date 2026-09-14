using System.Text;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0439: the "Not delivered" section of a shortfall run — in the pull request body and in
/// the ticket comment, the two places the outcome is read. It names every phase the run
/// left, in the derivation's own words, and the one reason the run stopped, so a reader
/// decides "merge what is here" or "send it back" without opening the run.
/// </summary>
public static class ShortfallSection
{
    public const string Heading = "## Not delivered";

    /// <summary>Empty when the run is not a shortfall, so a caller interpolates unconditionally.</summary>
    public static string Build(RunShortfall? shortfall)
    {
        if (shortfall is null) return string.Empty;
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine(Heading);
        sb.AppendLine();
        sb.AppendLine(
            $"{shortfall.Delivered.Count} of {shortfall.PhaseCount} phase(s) are built, verified and on this "
            + $"branch; the run stopped before the rest: {shortfall.Reason}");
        sb.AppendLine();
        foreach (var phase in shortfall.NotDelivered)
            sb.AppendLine($"- **{phase.PhaseId}** — {phase.Goal} ({Standing(phase)})");
        sb.AppendLine();
        sb.AppendLine(
            "_Nothing of these phases is on the branch: work the stopped phase had begun was "
            + "reverted to the last verified commit._");
        return sb.ToString().TrimEnd();
    }

    private static string Standing(PhaseProgress phase) => phase.State switch
    {
        PhaseRunState.Failed => $"failed: {phase.FailingCommand ?? "verification red"}",
        PhaseRunState.InProgress => "started, not finished",
        _ => "not started",
    };
}
