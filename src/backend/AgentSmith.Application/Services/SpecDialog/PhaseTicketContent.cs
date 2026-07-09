namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// p0315c: one rendered phase ticket — title plus the markdown body whose
/// LAST fenced block is the machine-readable ```yaml phase spec.
/// </summary>
public sealed record PhaseTicketContent(string Title, string Body);
