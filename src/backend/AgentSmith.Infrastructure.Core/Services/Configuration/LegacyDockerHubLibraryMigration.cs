using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// 2026-11-16-f3fbd: folds the retired <c>sandbox.allow_docker_hub_library</c> switch into
/// <c>sandbox.allowed_registries</c>, keeping the boundary each pair meant now that the library is the
/// entry <c>docker.io/library/</c> and part of the default. True beside a named list appends the entry;
/// false beside an empty list names the two other defaults, so the library stays refused. Every other
/// pair already means what the list alone says, so only the key goes. Runs on every load, save and
/// import, so a file or a stored document that still carries the key keeps the boundary it had.
/// </summary>
public sealed class LegacyDockerHubLibraryMigration
{
    private const string Library = "docker.io/library/";

    /// <summary>The block, its switch folded in and cleared.</summary>
    public SandboxGlobalConfig Apply(SandboxGlobalConfig sandbox)
    {
        ArgumentNullException.ThrowIfNull(sandbox);
        var library = sandbox.AllowDockerHubLibrary;
        sandbox.AllowDockerHubLibrary = null;
        var named = sandbox.AllowedRegistries.Any(r => !string.IsNullOrWhiteSpace(r));
        if (library == true && named && !Lists(sandbox, Library))
            sandbox.AllowedRegistries = [.. sandbox.AllowedRegistries, Library];
        else if (library == false && !named)
            sandbox.AllowedRegistries = ["mcr.microsoft.com/", "ghcr.io/"];
        return sandbox;
    }

    private static bool Lists(SandboxGlobalConfig sandbox, string entry) =>
        sandbox.AllowedRegistries.Any(r => string.Equals(r.Trim(), entry, StringComparison.Ordinal));
}
