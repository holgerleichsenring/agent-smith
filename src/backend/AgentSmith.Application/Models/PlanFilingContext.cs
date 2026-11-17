using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Models;

/// <summary>2026-11-17-5701b: context for the PlanFiling step — the run's project, the one the filing resolves to.</summary>
public sealed record PlanFilingContext(PipelineContext Pipeline, ResolvedProject Project) : ICommandContext;
