namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// 2026-10-02-5f89a: the secrets-catalog name and the environment variable a host type
/// authenticated with while every entity of that type shared one token.
/// </summary>
public sealed record LegacyCredentialKey(string Secret, string Variable);
