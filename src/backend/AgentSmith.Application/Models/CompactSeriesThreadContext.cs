using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Models;

/// <summary>2026-11-25-433fb: context for the CompactSeriesThread step.</summary>
public sealed record CompactSeriesThreadContext(PipelineContext Pipeline, ResolvedProject Project) : ICommandContext;
