namespace AgentSmith.Contracts.Services;

/// <summary>
/// Single source of truth for the names of tools whose results must be
/// scrubbed from the conversation history after one round-trip (p0191).
/// Lives in Contracts so the Infrastructure-level history-scrub client
/// and the Application-level tool host can both reference the same string
/// without a layering inversion.
/// </summary>
public static class SensitiveToolNames
{
    public const string GetArtifactCredentials = "get_artifact_credentials";

    public static IReadOnlySet<string> All { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            GetArtifactCredentials,
        };
}
