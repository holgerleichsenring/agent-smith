namespace AgentSmith.Dispatcher.Models;

/// <summary>
/// The available command types in the Agent Smith Slack modal.
/// </summary>
public enum ModalCommandType
{
    FixBug,
    FixBugNoTests,
    AddFeature,
    SecurityReview,
    MadDiscussion,
    LegalAnalysis,
    ListTickets,
    CreateTicket,
    InitProject
}
