using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// 2026-10-01-283dc: builds <see cref="ReferenceSetSandbox"/> instances over the same spawn path
/// and the same hold register a repository scope uses. The hold is keyed by the conversation, the
/// address and the set id, so a set re-uploaded under the same name is a different container.
/// </summary>
public sealed class ReferenceSetSandboxFactory(
    SourceScopeOpener opener,
    ReferenceSetMaterialiser materialiser,
    IHeldSandboxRegister holds,
    ILogger<ReferenceSetSandbox> logger) : IReferenceSetSandboxFactory
{
    public ISourceScopeSandbox Create(
        ResolvedProject project, string conversationId, string address, string setId) =>
        new ReferenceSetSandbox(project, conversationId, address, setId,
            new SourceScopeHold(holds, conversationId, address, setId, logger),
            opener, materialiser, logger);
}
