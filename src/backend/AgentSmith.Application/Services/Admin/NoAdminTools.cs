using AgentSmith.Contracts.Commands;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Admin;

/// <summary>2026-11-17-f3a8a: the surface of a composition without the server's admin tools — the turn answers from
/// the conversation alone.</summary>
public sealed class NoAdminTools : IAdminToolSurface
{
    public IList<AITool> Tools(PipelineContext pipeline) => [];
}
