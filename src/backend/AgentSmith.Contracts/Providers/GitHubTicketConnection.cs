namespace AgentSmith.Contracts.Providers;

/// <summary>
/// GitHub ticket-provider credentials. Shared by GitHubTicketProvider,
/// GitHubTicketStatusTransitioner, and the internal GitHubIssueLister.
/// 2026-11-25-0873: <c>SelfLogin</c> is the login the token acts as when <c>GET /user</c> cannot say it — a GitHub App's
/// <c>{slug}[bot]</c>, read on first use; null for a user token.
/// </summary>
public sealed record GitHubTicketConnection(
    string RepoUrl,
    string Token,
    Tickets.TicketLabelVocabulary? Labels = null,
    Func<string?>? SelfLogin = null)
{
    /// <summary>2026-09-25-3c7ac: what this board calls the labels the framework writes. Null is
    /// today's vocabulary, which is what a tracker configuring nothing gets.</summary>
    public Tickets.TicketLabelVocabulary ResolvedLabels => Labels ?? Tickets.TicketLabelVocabulary.Default;
}
