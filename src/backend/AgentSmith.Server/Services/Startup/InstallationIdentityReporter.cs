using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Diagnostics;

namespace AgentSmith.Server.Services.Startup;

/// <summary>
/// 2026-08-27-729e: collects what this installation is running into one answer. The numbers
/// all existed already and were readable nowhere: the server's build was carried solely to
/// be COMPARED against a caller's, the agent version was resolved only while spawning, and
/// the pending-migration count was computed only to be spent on a probe's status sentence.
/// So a version was visible exactly when it was wrong and never when somebody simply wanted
/// to know it.
/// <para>
/// It reports and does not judge. Whether the halves disagree is already answered — by
/// <see cref="BuildMismatchDetector"/> for the caller's bundle and by
/// <see cref="PinnedAgentProbe"/> for a project pinned away from this release — and a second
/// opinion on the same facts would be a second thing to keep true.
/// </para>
/// </summary>
public sealed class InstallationIdentityReporter(
    BuildIdentity build,
    AgentSmithConfig config,
    IAgentVersionResolver versions,
    IPersistenceStateReader persistence,
    ILogger<InstallationIdentityReporter> logger)
{
    public async Task<InstallationIdentityResponse> ReadAsync(CancellationToken cancellationToken)
    {
        var database = await persistence.ReadPersistenceStateAsync(cancellationToken);
        return new InstallationIdentityResponse(
            Stated(build.Version),
            Stated(build.Revision),
            [.. config.Projects.Select(p => AgentOf(p.Key, p.Value))],
            new DatabaseIdentity(
                config.Persistence.Provider, database.Reachable,
                database.PendingMigrations, database.Error));
    }

    private SandboxAgentRelease AgentOf(string project, ResolvedProject resolved)
    {
        try
        {
            var choice = versions.Resolve(resolved);
            return new SandboxAgentRelease(
                project, choice.Version,
                choice.IsPinned ? SandboxAgentRelease.Pinned : SandboxAgentRelease.Derived);
        }
        catch (InvalidOperationException ex)
        {
            // The resolver's only fail-loud: a build carrying no release has nothing to
            // derive from. PinnedAgentProbe swallows the same throw for the same reason —
            // it is a configuration fault the configuration probe already reports — and
            // here it is a LINE, because "this build cannot say" is the honest answer.
            logger.LogDebug(ex, "The sandbox agent version for project {Project} cannot be derived", project);
            return new SandboxAgentRelease(project, null, SandboxAgentRelease.Underivable);
        }
    }

    /// <summary>A half that was not stamped says nothing, and an empty string is not an answer.</summary>
    private static string? Stated(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
