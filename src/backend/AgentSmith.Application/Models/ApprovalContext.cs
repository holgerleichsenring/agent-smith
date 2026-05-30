using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Models;

/// <summary>
/// Context for requesting user approval before code-changing work begins.
/// Plan is nullable since p0179f — collapsed coding presets (fix-bug,
/// add-feature, fix-no-test) reach Approval without a structured Plan
/// because the agentic master owns planning internally.
/// </summary>
public sealed record ApprovalContext(
    Plan? Plan,
    PipelineContext Pipeline) : ICommandContext;
