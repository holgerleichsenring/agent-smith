namespace AgentSmith.Contracts.Constants;

/// <summary>
/// Pairs an in-container environment-variable name with the corresponding
/// key inside the operator-managed Kubernetes Secret (kebab-case).
/// <see cref="AgentSecretNames"/> derives the forwarded secret names from <see cref="All"/>.
/// </summary>
public sealed record AgentSecretBinding(string EnvVar, string K8sSecretKey)
{
    /// <summary>The full set of secret-backed env-vars the agent runtime reads.</summary>
    public static readonly IReadOnlyList<AgentSecretBinding> All =
    [
        new(AgentEnvKeys.AnthropicApiKey, "anthropic-api-key"),
        new(AgentEnvKeys.OpenAiApiKey, "openai-api-key"),
        new(AgentEnvKeys.AzureOpenAiApiKey, "azure-openai-api-key"),
        new(AgentEnvKeys.GeminiApiKey, "gemini-api-key"),
        new(AgentEnvKeys.GroqApiKey, "groq-api-key"),
        new(AgentEnvKeys.CopilotGitHubToken, "copilot-github-token"),
        new(AgentEnvKeys.GitHubToken, "github-token"),
        new(AgentEnvKeys.GitLabToken, "gitlab-token"),
        new(AgentEnvKeys.AzureDevOpsToken, "azure-devops-token"),
        new(AgentEnvKeys.JiraToken, "jira-token"),
        new(AgentEnvKeys.JiraEmail, "jira-email"),
        new(AgentEnvKeys.RedisUrl, "redis-url"),
    ];
}
