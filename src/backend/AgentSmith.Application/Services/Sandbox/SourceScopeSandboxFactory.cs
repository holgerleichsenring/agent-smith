using AgentSmith.Application.Services.Builders;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// p0315b: builds lazy read-only <see cref="SourceScopeSandbox"/> instances
/// over the production <see cref="ISandboxFactory"/> — same spawn path, same
/// spec builder; the only differences are the deferred spawn, the generic
/// no-toolchain image, and the read-only step guard.
/// </summary>
public sealed class SourceScopeSandboxFactory(
    ISandboxFactory sandboxFactory,
    SandboxSpecBuilder specBuilder,
    IRunContextAccessor runContext,
    ILogger<SourceScopeSandbox> sandboxLogger) : ISourceScopeSandboxFactory
{
    public ISourceScopeSandbox Create(ResolvedProject project, RepoConnection repo) =>
        new SourceScopeSandbox(project, repo, sandboxFactory, specBuilder, runContext, sandboxLogger);
}
