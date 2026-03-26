namespace AgentSmith.Contracts.Models;

/// <summary>
/// Maps a secret pattern to its cloud provider and revocation URL.
/// </summary>
public sealed record SecretProvider(string Name, string RevokeUrl);
