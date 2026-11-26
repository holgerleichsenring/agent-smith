using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Models;

/// <summary>2026-11-25-96aea: context for the OpenTicketSeries step, the code run's step after RunPreflight.</summary>
public sealed record OpenTicketSeriesContext(PipelineContext Pipeline, ResolvedProject Project) : ICommandContext;
