using AgentSmith.Application.Services.Builders;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// Spawns the sandbox a source scope reads through and lands it on the scope's revision.
/// A sandbox that spawned but could not be prepared is torn down here, so a caller that sees
/// the exception holds nothing it must dispose.
/// <para>
/// The sandbox spec comes from the RUN'S project and carries the run id read at this moment,
/// so the reaper sees the run that opened it.
/// </para>
/// </summary>
public sealed class SourceScopeOpener(
    SourceScopeMaterialiser materialiser,
    ISandboxFactory sandboxFactory,
    SandboxSpecBuilder specBuilder,
    IRunContextAccessor runContext)
{
    /// <summary>Returns the prepared sandbox and the sha it landed on.</summary>
    public async Task<(ISandbox Sandbox, string Sha)> OpenAsync(
        ResolvedProject project, RepoConnection repo, string? revision, CancellationToken ct)
    {
        // language/pipelineName null → generic git-bearing image, p0320a light profile.
        var spec = specBuilder.Build(project, language: null, pipelineName: null)
            with { RunId = runContext.CurrentRunId };
        var created = await sandboxFactory.CreateAsync(spec, ct);
        try
        {
            return (created, await materialiser.PrepareAsync(created, repo, revision, ct));
        }
        catch
        {
            await created.DisposeAsync();
            throw;
        }
    }
}
