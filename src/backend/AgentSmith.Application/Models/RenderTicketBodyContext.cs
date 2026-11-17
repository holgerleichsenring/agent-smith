using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Models;

/// <summary>2026-11-17-dd05b: context for the RenderTicketBody step. <paramref name="Project"/> carries the run's
/// repositories (ContextKeys.Repos), so the branch is read and stamped where a run reads it.</summary>
public sealed record RenderTicketBodyContext(PipelineContext Pipeline, ResolvedProject Project) : ICommandContext;
