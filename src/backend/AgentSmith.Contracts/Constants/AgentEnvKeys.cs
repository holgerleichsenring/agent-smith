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

    /// <summary>
    /// 2026-09-07-d5f2: the Copilot seat token. The SDK resolves COPILOT_GITHUB_TOKEN, then
    /// GH_TOKEN, then GITHUB_TOKEN; an agent may name its own variable through
    /// <c>api_key_secret</c> instead, which is how two agents answer on two seats. Copilot
    /// rejects org-owned PATs, so a seat is always a person's.
    /// </summary>
    public const string CopilotGitHubToken = "COPILOT_GITHUB_TOKEN";

    // Source / ticket providers
    public const string GitHubToken = "GITHUB_TOKEN";
    public const string GitLabToken = "GITLAB_TOKEN";
    public const string AzureDevOpsToken = "AZURE_DEVOPS_TOKEN";
    public const string JiraToken = "JIRA_TOKEN";
    public const string JiraEmail = "JIRA_EMAIL";

    // Infrastructure
    public const string RedisUrl = "REDIS_URL";

    /// <summary>
    /// 2026-09-07-d5f2: where the Copilot CLI runtime lives. The build deliberately does not
    /// download it (see the GitHub.Copilot.SDK note in AgentSmith.Infrastructure.csproj), so the
    /// image that runs a copilot agent places the binary and points this at it. Unset means the
    /// SDK's bundled runtime, which only exists in a build that fetched it.
    /// </summary>
    public const string CopilotCliPath = "COPILOT_CLI_PATH";
}
