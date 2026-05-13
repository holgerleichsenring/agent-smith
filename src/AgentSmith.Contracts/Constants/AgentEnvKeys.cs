namespace AgentSmith.Contracts.Constants;

/// <summary>
/// Canonical environment-variable names used across the agent runtime —
/// LLM provider tokens, source-provider tokens, ticket-provider credentials,
/// and the Redis bus endpoint. Replaces magic strings in spawners
/// (KubernetesJobSpawner, DockerJobSpawner) and chat-client builders.
/// </summary>
public static class AgentEnvKeys
{
    // LLM providers
    public const string AnthropicApiKey = "ANTHROPIC_API_KEY";
    public const string OpenAiApiKey = "OPENAI_API_KEY";
    public const string AzureOpenAiApiKey = "AZURE_OPENAI_API_KEY";
    public const string GeminiApiKey = "GEMINI_API_KEY";
    public const string GroqApiKey = "GROQ_API_KEY";

    // Source / ticket providers
    public const string GitHubToken = "GITHUB_TOKEN";
    public const string GitLabToken = "GITLAB_TOKEN";
    public const string AzureDevOpsToken = "AZURE_DEVOPS_TOKEN";
    public const string JiraToken = "JIRA_TOKEN";
    public const string JiraEmail = "JIRA_EMAIL";

    // Infrastructure
    public const string RedisUrl = "REDIS_URL";
}
