namespace AgentSmith.Contracts.Providers;

/// <summary>
/// GitHub ticket-provider credentials. Shared by GitHubTicketProvider,
/// GitHubTicketStatusTransitioner, and the internal GitHubIssueLister.
/// </summary>
public sealed record GitHubTicketConnection(
    string RepoUrl,
    string Token,
    Tickets.TicketLabelVocabulary? Labels = null)
{
    /// <summary>2026-09-25-3c7ac: what this board calls the labels the framework writes. Null is
    /// today's vocabulary, which is what a tracker configuring nothing gets.</summary>
    public Tickets.TicketLabelVocabulary ResolvedLabels => Labels ?? Tickets.TicketLabelVocabulary.Default;
}
