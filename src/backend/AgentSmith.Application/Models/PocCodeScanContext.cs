using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Models;

/// <summary>2026-11-23-933dc: context for the PocCodeScan step — the poc- project (its agent explains a blocked verdict)
/// and the run's seeds.</summary>
public sealed record PocCodeScanContext(PipelineContext Pipeline, ResolvedProject Project, PocSeeds Seeds) : ICommandContext;
