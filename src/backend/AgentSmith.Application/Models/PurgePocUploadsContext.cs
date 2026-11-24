using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.Models;

/// <summary>2026-11-23-c81ee: context for the PurgePocUploads step — the installation run's pipeline, for its run id.</summary>
public sealed record PurgePocUploadsContext(PipelineContext Pipeline) : ICommandContext;
