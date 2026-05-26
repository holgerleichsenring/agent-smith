namespace AgentSmith.Contracts.Models;

/// <summary>
/// A finding discovered by the autonomous observation pipeline.
/// Represents an improvement suggestion agreed upon by multiple skill roles.
/// </summary>
public sealed record AutonomousFinding(
    string Title,
    string Description,
    string Category,
    int Confidence,
    string FoundByRole,
    IReadOnlyList<string> AgreedByRoles,
    string? TicketUrl);
