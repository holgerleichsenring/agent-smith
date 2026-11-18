using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Models;

/// <summary>2026-11-17-0207a: context for the OpenSeriesRun step, the 'file' run's last.</summary>
public sealed record OpenSeriesRunContext(PipelineContext Pipeline, ResolvedProject Project) : ICommandContext;
