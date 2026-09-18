namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-09-17-042el: what an operator's answer to an approval question decided. Recorded on the
/// transcript turn that answered, so the dashboard shows a decision rather than the word.
/// </summary>
public enum SpecDialogDecision
{
    Approved = 0,
    Rejected = 1,
}
