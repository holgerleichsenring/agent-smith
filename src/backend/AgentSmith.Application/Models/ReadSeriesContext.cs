using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Models;

/// <summary>2026-11-17-0207a: context for the ReadSeries step, a beat's first.</summary>
public sealed record ReadSeriesContext(PipelineContext Pipeline, ResolvedProject Project) : ICommandContext;
