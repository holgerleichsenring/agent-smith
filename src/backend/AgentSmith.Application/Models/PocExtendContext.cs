using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Models;

/// <summary>2026-11-23-c81ee: context for the PocExtend step — the poc- project and the seeds the extend route sent.</summary>
public sealed record PocExtendContext(PipelineContext Pipeline, ResolvedProject Project, PocSeeds Seeds) : ICommandContext;
