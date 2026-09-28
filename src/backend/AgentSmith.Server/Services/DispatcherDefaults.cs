namespace AgentSmith.Server.Services;

/// <summary>
/// Central repository for all default values used across the Dispatcher.
/// Eliminates magic strings and satisfies Convention over Configuration:
/// sensible defaults work out of the box without any environment variables.
/// </summary>
internal static class DispatcherDefaults
{
    // --- Redis ---
    public const string RedisUrl = "localhost:6379";

    // --- Platforms ---
    public const string PlatformSlack = "slack";
    public const string PlatformTeams = "teams";

    // 2026-09-15-9033: the dashboard's own spec-dialog channel. Distinct from the two
    // above by necessity — SpecDialogMessenger keys its adapter dictionary on the
    // platform name and throws on a duplicate, which would take the spec dialog down at
    // startup rather than in the one conversation that collided.
    public const string PlatformDashboard = "dashboard";

    // --- Config ---
    public const string ConfigPath = "config/agentsmith.yml";

    // --- Slack API ---
    public const string SlackTimestampHeader = "X-Slack-Request-Timestamp";
    public const string SlackSignatureHeader = "X-Slack-Signature";
    public const string SlackSignaturePrefix = "v0=";
    public const int SlackReplayWindowSeconds = 300;

    // --- Slack Modals ---
    public const string SlackModalCallbackId = "agentsmith_command";
    public const string SlackBlockCommand = "command_select";
    public const string SlackBlockProject = "project_select";
    public const string SlackBlockTicket = "ticket_select";
    public const string SlackBlockTitle = "ticket_title";
    public const string SlackBlockDescription = "ticket_description";
    public const string SlackActionCommand = "command_action";
    public const string SlackActionProject = "project_action";
    public const string SlackActionTicket = "ticket_action";
}
