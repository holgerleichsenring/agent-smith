using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services;

/// <summary>
/// Result of executing a single command inside a parallel batch — captures the command,
/// its CommandResult (or null if the slot was cancelled before execution), elapsed time,
/// and the global step index for logging and execution-trail accounting.
/// </summary>
public sealed record BatchSlot(
    PipelineCommand Command,
    CommandResult Result,
    TimeSpan Elapsed,
    int StepIndex);
