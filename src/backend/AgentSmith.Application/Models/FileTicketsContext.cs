using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Models;

/// <summary>2026-11-17-5701b: context for the FileTickets step. <paramref name="Project"/> carries the run's
/// repositories (ContextKeys.Repos) — the list CheckoutSource clones — so a branch write lands where a run reads.</summary>
public sealed record FileTicketsContext(PipelineContext Pipeline, ResolvedProject Project) : ICommandContext;
