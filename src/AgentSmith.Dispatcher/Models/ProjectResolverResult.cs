namespace AgentSmith.Dispatcher.Services;

/// <summary>
/// Discriminated union of project resolution outcomes.
/// </summary>
public abstract record ProjectResolverResult;

/// <summary>Ticket was found in exactly one project.</summary>
public sealed record ProjectResolved(string ProjectName) : ProjectResolverResult;

/// <summary>Ticket was not found in any configured project.</summary>
public sealed record ProjectNotFound(string TicketNumber) : ProjectResolverResult;

/// <summary>Ticket was found in multiple projects — user must pick one.</summary>
public sealed record ProjectAmbiguous(
    string TicketNumber, IReadOnlyList<string> Projects) : ProjectResolverResult;
