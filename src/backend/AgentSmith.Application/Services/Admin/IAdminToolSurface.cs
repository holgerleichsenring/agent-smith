using AgentSmith.Contracts.Commands;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Admin;

/// <summary>
/// 2026-11-17-f3a8a: the tools an admin assistant turn works with. The server supplies them — they read and change
/// the installation's own records and settings, which only the server holds.
/// </summary>
public interface IAdminToolSurface
{
    IList<AITool> Tools(PipelineContext pipeline);
}
