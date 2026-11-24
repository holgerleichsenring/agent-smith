using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Models;

/// <summary>2026-11-23-933dg: context for the WritePocVariables step — the run's project and its pipeline, whose seed
/// the step removes as soon as it is read.</summary>
public sealed record WritePocVariablesContext(PipelineContext Pipeline, ResolvedProject Project) : ICommandContext;
