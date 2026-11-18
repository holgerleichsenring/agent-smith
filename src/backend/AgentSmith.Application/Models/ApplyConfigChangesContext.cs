using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Models;

/// <summary>2026-11-17-f3a8c: context for the ApplyConfigChanges step — the installation and the run's seeds.</summary>
public sealed record ApplyConfigChangesContext(PipelineContext Pipeline, ResolvedProject Project) : ICommandContext;
