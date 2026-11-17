using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Models;

/// <summary>2026-11-17-e061c: context for the AmendTicket step. <paramref name="Project"/> carries the run's
/// repositories (ContextKeys.Repos), so the branch write lands where a run reads.</summary>
public sealed record AmendTicketContext(PipelineContext Pipeline, ResolvedProject Project) : ICommandContext;
