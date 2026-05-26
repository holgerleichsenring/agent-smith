using AgentSmith.Application.Services.Loop;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Services.Prompts;

/// <summary>
/// Result of <see cref="PlanParser.ParseStrict"/>. Plan is null when Validation
/// is a Failure; callers (RetryCoordinator hand-off, run-result rendering) decide
/// what to do with the structured failure message.
/// </summary>
public sealed record PlanParseResult(Plan? Plan, ValidationResult Validation);
