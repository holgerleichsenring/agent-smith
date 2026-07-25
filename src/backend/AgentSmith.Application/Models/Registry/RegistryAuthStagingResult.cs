namespace AgentSmith.Application.Models.Registry;

/// <summary>
/// The LLM fallback's output: the global auth-config files to write (with
/// placeholder tokens) plus the registry hosts it targeted, for host-side
/// substitution and logging.
/// </summary>
public sealed record RegistryAuthStagingResult(
    IReadOnlyList<StagedAuthFile> Files,
    IReadOnlyList<string> TargetedHosts)
{
    public static RegistryAuthStagingResult Empty { get; } =
        new(Array.Empty<StagedAuthFile>(), Array.Empty<string>());
}
