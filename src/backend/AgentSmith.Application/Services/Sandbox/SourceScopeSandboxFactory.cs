using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// p0315b: builds lazy read-only <see cref="SourceScopeSandbox"/> instances
/// over the production <see cref="ISandboxFactory"/> — same spawn path, same
/// spec builder; the only differences are the deferred spawn, the generic
/// no-toolchain image, and the read-only step guard.
/// <para>
/// 2026-09-13-9802: the sandbox spec still comes from the RUN'S project, not from
/// whichever project owns the repository. The sandbox is the run's cost and the run's
/// isolation; a foreign repository contributes a clone url and a revision, nothing else.
/// </para>
/// </summary>
public sealed class SourceScopeSandboxFactory(
    SourceScopeOpener opener,
    ISourceScopeObserverAccessor observers,
    IHeldSandboxRegister holds,
    ILogger<SourceScopeSandbox> sandboxLogger) : ISourceScopeSandboxFactory
{
    public ISourceScopeSandbox Create(
        ResolvedProject project, RepoConnection repo, string? revision = null,
        string? conversationId = null) =>
        new SourceScopeSandbox(project, repo, revision, Hold(conversationId, repo, revision),
            opener, observers, sandboxLogger);

    // 2026-09-22-2d11b: a scope that names no conversation carries no hold, takes nothing
    // back and disposes what it spawned — which is every caller but a design turn.
    private SourceScopeHold? Hold(string? conversationId, RepoConnection repo, string? revision) =>
        string.IsNullOrEmpty(conversationId)
            ? null
            : new SourceScopeHold(holds, conversationId, repo, revision, sandboxLogger);
}
