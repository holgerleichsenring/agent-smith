namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>2026-11-12-a1fa: what holds a session's turn gate. Only a design turn the router runs takes messages
/// mid-turn; the ticket steward's turn and every other hold (a resume, a deletion) answer that a turn is running.</summary>
public enum SpecDialogGateHold
{
    Other,
    DesignTurn,
    StewardTurn,
}
