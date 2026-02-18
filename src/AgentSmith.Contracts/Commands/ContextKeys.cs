namespace AgentSmith.Contracts.Commands;

/// <summary>
/// Well-known keys for the PipelineContext dictionary.
/// </summary>
public static class ContextKeys
{
    public const string TicketId = "TicketId";
    public const string Ticket = "Ticket";
    public const string Repository = "Repository";
    public const string Plan = "Plan";
    public const string CodeChanges = "CodeChanges";
    public const string CodeAnalysis = "CodeAnalysis";
    public const string CodingPrinciples = "CodingPrinciples";
    public const string Approved = "Approved";
    public const string TestResults = "TestResults";
    public const string PullRequestUrl = "PullRequestUrl";
}
