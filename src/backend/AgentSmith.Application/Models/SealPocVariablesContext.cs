using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Models;

/// <summary>2026-11-23-933dg: context for the SealPocVariables step — the poc- project and the run's seeds.</summary>
public sealed record SealPocVariablesContext(PipelineContext Pipeline, ResolvedProject Project, PocSeeds Seeds) : ICommandContext;
