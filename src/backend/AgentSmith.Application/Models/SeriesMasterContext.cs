using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Models;

/// <summary>2026-11-17-0207a: context for the SeriesMaster step.</summary>
public sealed record SeriesMasterContext(PipelineContext Pipeline, ResolvedProject Project) : ICommandContext;
