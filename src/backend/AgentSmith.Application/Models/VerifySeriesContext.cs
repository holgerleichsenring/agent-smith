using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Models;

/// <summary>2026-11-17-0207d: context for the VerifySeries step of a series-verify run.</summary>
public sealed record VerifySeriesContext(PipelineContext Pipeline, ResolvedProject Project) : ICommandContext;
