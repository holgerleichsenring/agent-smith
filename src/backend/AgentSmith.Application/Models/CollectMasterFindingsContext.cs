using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.Models;

/// <summary>
/// p0267: context for the discrete CollectMasterFindings step that sits between
/// AgenticMaster and DeliverFindings on the api-security pipeline. Carries only the
/// pipeline — the master's answer + skill name are read from context keys the
/// AgenticMaster step published.
/// </summary>
public sealed record CollectMasterFindingsContext(
    PipelineContext Pipeline) : ICommandContext;
