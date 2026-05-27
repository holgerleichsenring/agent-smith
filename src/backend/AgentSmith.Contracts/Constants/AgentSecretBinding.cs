namespace AgentSmith.Contracts.Constants;

/// <summary>
/// Pairs an in-container environment-variable name with the corresponding
/// key inside the operator-managed Kubernetes Secret (kebab-case).
/// KubernetesJobSpawner iterates <see cref="All"/> to project each pair into
/// a V1EnvVar that references the secret. All bindings are Optional=true so
/// missing keys do not block pod creation.
/// </summary>
public sealed record AgentSecretBinding(string EnvVar, string K8sSecretKey)
{
    /// <summary>The full set of secret-backed env-vars forwarded to spawned agent pods.</summary>
    public static readonly IReadOnlyList<AgentSecretBinding> All =
    [
        new(AgentEnvKeys.AnthropicApiKey, "anthropic-api-key"),
        new(AgentEnvKeys.OpenAiApiKey, "openai-api-key"),
        new(AgentEnvKeys.AzureOpenAiApiKey, "azure-openai-api-key"),
        new(AgentEnvKeys.GeminiApiKey, "gemini-api-key"),
        new(AgentEnvKeys.GroqApiKey, "groq-api-key"),
        new(AgentEnvKeys.GitHubToken, "github-token"),
        new(AgentEnvKeys.GitLabToken, "gitlab-token"),
        new(AgentEnvKeys.AzureDevOpsToken, "azure-devops-token"),
        new(AgentEnvKeys.JiraToken, "jira-token"),
        new(AgentEnvKeys.JiraEmail, "jira-email"),
        new(AgentEnvKeys.RedisUrl, "redis-url"),
    ];
}
