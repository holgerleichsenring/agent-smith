using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Models;

/// <summary>2026-11-17-f3a8d: context for the WriteSecret step — the installation and the run's seeds.</summary>
public sealed record WriteSecretContext(PipelineContext Pipeline, ResolvedProject Project) : ICommandContext;
