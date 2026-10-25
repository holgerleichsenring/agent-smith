namespace AgentSmith.Infrastructure.Services.Sandbox;

/// <summary>
/// 2026-10-25-c5a7h: credentials the server process holds in its environment that no
/// <see cref="Contracts.Constants.AgentSecretBinding"/> names: the database connection, webhook
/// secrets, chat tokens and the keys some builders read straight from the environment. An
/// in-process step never inherits them. A test pins this list to the server manifest, so the
/// next secret added there fails until it is declared here.
/// </summary>
public static class ServerSecretEnvNames
{
    public static IReadOnlyList<string> All =>
    [
        "REDIS_URL", "SANDBOX_REDIS_URL", "AGENTSMITH_PERSISTENCE_CONNECTION",
        "GITHUB_WEBHOOK_SECRET", "GITLAB_WEBHOOK_TOKEN", "AZDO_WEBHOOK_SECRET", "JIRA_WEBHOOK_SECRET",
        "SLACK_BOT_TOKEN", "SLACK_SIGNING_SECRET", "TEAMS_APP_PASSWORD", "GH_TOKEN", "GOOGLE_API_KEY",
    ];
}
