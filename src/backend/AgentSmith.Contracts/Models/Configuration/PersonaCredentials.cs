namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// Credentials for a single API persona (e.g. user1, admin).
/// </summary>
public sealed class PersonaCredentials
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
