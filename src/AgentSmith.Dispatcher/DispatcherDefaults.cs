namespace AgentSmith.Dispatcher;

/// <summary>
/// Central repository for all default values used across the Dispatcher.
/// Eliminates magic strings and satisfies Convention over Configuration:
/// sensible defaults work out of the box without any environment variables.
/// </summary>
internal static class DispatcherDefaults
{
    // --- Redis ---
    public const string RedisUrl = "localhost:6379";

    // --- Spawner ---
    public const string SpawnerType = "kubernetes";
    public const string SpawnerTypeDocker = "docker";

    // --- Agent image ---
    public const string AgentImage = "agentsmith:latest";
    public const string ImagePullPolicy = "IfNotPresent";

    // --- Kubernetes ---
    public const string K8sNamespace = "default";
    public const string K8sSecretName = "agentsmith-secrets";
    public const string K8sApiPatch = "https://host.docker.internal:";
    public const string K8sApiLocal = "https://127.0.0.1:";

    // --- Docker ---
    public const string DockerSocketUnix = "unix:///var/run/docker.sock";
    public const string DockerSocketWindows = "npipe://./pipe/docker_engine";

    // --- Platforms ---
    public const string PlatformSlack = "slack";
    public const string PlatformTeams = "teams";

    // --- Config ---
    public const string ConfigPath = "config/agentsmith.yml";

    // --- Slack API ---
    public const string SlackTimestampHeader = "X-Slack-Request-Timestamp";
    public const string SlackSignatureHeader = "X-Slack-Signature";
    public const string SlackSignaturePrefix = "v0=";
    public const int SlackReplayWindowSeconds = 300;
}
