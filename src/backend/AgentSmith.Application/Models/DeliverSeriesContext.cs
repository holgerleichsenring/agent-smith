using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Models;

/// <summary>2026-11-24-f9c5e: context for the DeliverSeries step of a beat.</summary>
public sealed record DeliverSeriesContext(PipelineContext Pipeline, ResolvedProject Project) : ICommandContext;
