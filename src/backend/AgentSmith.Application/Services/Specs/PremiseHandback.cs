using System.Text;
using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-17-0e79c: what a run SAYS when a phase's stated premise no longer holds — the one
/// verdict string the phase row carries, and the notice the ticket gets.
/// <para>
/// The verdict is composed into a SINGLE string on purpose.
/// <see cref="PhaseProgressRecorder"/> publishes <c>failingCommand ?? note</c>, so passing the
/// premise as one and the evidence as the other would drop the evidence silently. The premise,
/// the finding, the minted line and WHAT WAS LOOKED AT travel together or not at all: the step
/// from a look to a premise is a judgement, and a reader who cannot see the look cannot make it.
/// </para>
/// </summary>
public static class PremiseHandback
{
    /// <summary>Marks the notice as this system's own, so a later run does not read it as
    /// somebody answering. It carries no cut marker: that phrase is the anchor the comment
    /// rule measures from.</summary>
    public const string Heading = "## Agent Smith — a phase rests on something that is no longer so";

    /// <summary>What a notice for THIS phase carries, so the next run can tell it was already
    /// told and not say the same thing again.</summary>
    public static string Marker(string phaseId) => $"agent-smith:premise-check:{phaseId}";

    /// <summary>The phase row's verdict: the premise, why it fails, what was looked at.</summary>
    public static string Verdict(string phaseId, PremiseFinding finding)
    {
        ArgumentNullException.ThrowIfNull(finding);
        return $"False premise in {phaseId}: \"{Clean(finding.Premise)}\" — "
            + $"{Clean(finding.Why)}{Proof(finding)}";
    }

    /// <summary>The ticket's copy: every false premise, what proved it, and where a correction
    /// is made. <paramref name="hasPullRequest"/> decides which of the two true wordings for
    /// that last part is used — a first-phase hand-back has opened none.</summary>
    public static string Notice(
        PhaseDraft draft, IReadOnlyList<PremiseFinding> findings, bool hasPullRequest)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(findings);
        var sb = new StringBuilder().AppendLine(Heading).AppendLine();
        sb.AppendLine($"<!-- {Marker(draft.PhaseId)} -->").AppendLine();
        sb.AppendLine(
            $"Phase {draft.PhaseId} ({draft.Goal}) was not built. Before its work started, a "
            + "fresh instance checked what the specification says it rests on against the "
            + "repositories as they are now, and read otherwise:").AppendLine();
        foreach (var finding in findings)
        {
            sb.AppendLine($"- \"{Clean(finding.Premise)}\" — {Clean(finding.Why)}");
            if (!string.IsNullOrWhiteSpace(finding.Looked))
                sb.AppendLine($"  it looked at: {finding.Looked.Trim()}");
            if (!string.IsNullOrWhiteSpace(finding.Evidence))
                sb.AppendLine($"  {finding.Evidence.Trim()}");
        }
        sb.AppendLine();
        sb.AppendLine(
            "This run did not rewrite the specification and never will: the check reports, it "
            + "does not amend. Any phase that was already verified is still delivered.")
            .AppendLine();
        return sb.Append(hasPullRequest
            ? ApprovedSetKept.WhereToChangeIt
            : ApprovedSetKept.WhereToChangeItWithNoPullRequest).ToString();
    }

    // What proved it: the look, then the framework's own minted line for that look.
    private static string Proof(PremiseFinding finding)
    {
        var looked = string.IsNullOrWhiteSpace(finding.Looked)
            ? string.Empty : $" — it looked at {finding.Looked.Trim()}";
        var evidence = string.IsNullOrWhiteSpace(finding.Evidence)
            ? string.Empty : $" — {finding.Evidence.Trim()}";
        return looked + evidence;
    }

    private static string Clean(string? text) =>
        (text ?? string.Empty).Replace('\n', ' ').Replace('\r', ' ').Trim();
}
