using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Models;

/// <summary>2026-11-17-e061a: context for the CloseTicket step. <paramref name="Project"/> carries the run's
/// repositories (ContextKeys.Repos), the ones a pull request is closed through.</summary>
public sealed record CloseTicketContext(PipelineContext Pipeline, ResolvedProject Project) : ICommandContext;
