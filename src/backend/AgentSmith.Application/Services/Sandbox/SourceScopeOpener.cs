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
/// so the reaper sees the run that opened it. 2026-09-22-2d11a: and the conversation, when
/// the caller names one, so the reaper can tell a held scope from a corpse.
/// </para>
/// <para>
/// 2026-09-22-2d11b: spawning and preparing are two methods, because a HELD sandbox needs
/// the second without the first. A turn that took one back prepares it — which meets a tree
/// already there and refreshes it — and never reaches the spawn.
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
        ResolvedProject project, RepoConnection repo, string? revision,
        CancellationToken ct, string? conversationId = null)
    {
        var created = await SpawnAsync(project, ct, conversationId);
        try
        {
            return (created, await PrepareAsync(created, repo, revision, ct));
        }
        catch
        {
            await created.DisposeAsync();
            throw;
        }
    }

    /// <summary>The container, with nothing in it yet.</summary>
    public Task<ISandbox> SpawnAsync(
        ResolvedProject project, CancellationToken ct, string? conversationId = null)
    {
        // language/pipelineName null → generic git-bearing image, p0320a light profile.
        var spec = specBuilder.Build(project, language: null, pipelineName: null)
            with { RunId = runContext.CurrentRunId, ConversationId = conversationId };
        return sandboxFactory.CreateAsync(spec, ct);
    }

    /// <summary>
    /// The tree in it, and the sha it is on. The sandbox may be one this process just spawned
    /// or one a previous turn of the conversation left behind; the materialiser tells the two
    /// apart by what is in the work path, not by who handed it over.
    /// </summary>
    public Task<string> PrepareAsync(
        ISandbox sandbox, RepoConnection repo, string? revision, CancellationToken ct) =>
        materialiser.PrepareAsync(sandbox, repo, revision, ct);
}
