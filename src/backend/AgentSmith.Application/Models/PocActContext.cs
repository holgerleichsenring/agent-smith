using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Models;

/// <summary>2026-11-23-c81ed: context for the PocAct step — the poc- project, the run's preset and its seeds.</summary>
public sealed record PocActContext(PipelineContext Pipeline, ResolvedProject Project, string? Preset, PocSeeds Seeds) : ICommandContext;
