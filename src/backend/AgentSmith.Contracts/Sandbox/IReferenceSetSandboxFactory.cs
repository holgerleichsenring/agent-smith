using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Sandbox;

/// <summary>
/// 2026-10-01-283dc: builds the lazy, read-only sandbox an uploaded website is read through,
/// held for the conversation like a repository's.
/// </summary>
public interface IReferenceSetSandboxFactory
{
    /// <param name="address">The name the turn addresses it by — <c>reference:&lt;name&gt;</c>.</param>
    ISourceScopeSandbox Create(ResolvedProject project, string conversationId, string address, string setId);
}
