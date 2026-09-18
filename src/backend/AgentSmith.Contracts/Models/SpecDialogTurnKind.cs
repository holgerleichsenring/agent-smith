namespace AgentSmith.Contracts.Models;

/// <summary>
/// 2026-09-17-042ec: what an assistant turn of a design conversation produced, recorded when
/// the turn is kept rather than read back from its text. Only a recorded answer is a
/// discussion the operator can reply to; a failure note, a notice or a filing is not, and a
/// proposal is what the discussion has to come before.
/// </summary>
public enum SpecDialogTurnKind
{
    Answer = 0,
    BugProposal = 1,
    PhaseProposal = 2,
    EpicProposal = 3,
    Failure = 4,
    Notice = 5,
    Filing = 6,
}
