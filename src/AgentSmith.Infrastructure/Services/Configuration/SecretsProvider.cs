using AgentSmith.Domain.Exceptions;

namespace AgentSmith.Infrastructure.Services.Configuration;

/// <summary>
/// Reads secrets from environment variables.
/// </summary>
public sealed class SecretsProvider
{
    public string GetRequired(string envVarName)
    {
        return Environment.GetEnvironmentVariable(envVarName)
               ?? throw new ConfigurationException(
                   $"Required environment variable '{envVarName}' is not set.");
    }

    public string? GetOptional(string envVarName)
    {
        return Environment.GetEnvironmentVariable(envVarName);
    }
}
