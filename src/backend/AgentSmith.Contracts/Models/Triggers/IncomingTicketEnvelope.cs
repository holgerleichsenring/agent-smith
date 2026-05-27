namespace AgentSmith.Contracts.Models.Triggers;

/// <summary>
/// Platform-neutral shape of an incoming ticket — built by each webhook handler from its
/// payload type, then handed to ProjectResolver. All fields except Labels are optional so
/// the same record covers GitHub issues (Labels + SourceRepoUrl), ADO work items (Labels +
/// AreaPath), Jira issues (Labels), and (in p0141) emails (ToAddress).
/// </summary>
public sealed record IncomingTicketEnvelope
{
    public IReadOnlyList<string> Labels { get; init; } = [];
    public string? AreaPath { get; init; }
    public string? SourceRepoUrl { get; init; }
    public string? ToAddress { get; init; }
    public string? TicketId { get; init; }
    public string? TicketUrl { get; init; }
    public string? Platform { get; init; }
}
