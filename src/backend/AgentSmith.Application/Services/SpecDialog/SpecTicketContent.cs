namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// p0315c: one rendered spec ticket — title plus the markdown body whose
/// LAST fenced block is the machine-readable ```yaml spec.
/// </summary>
public sealed record SpecTicketContent(string Title, string Body);
