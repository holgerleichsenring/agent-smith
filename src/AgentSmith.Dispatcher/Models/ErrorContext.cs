namespace AgentSmith.Dispatcher.Models;

/// <summary>
/// Carries structured error context from the agent to the dispatcher.
/// Used to render actionable error messages with retry/logs/contact buttons.
/// </summary>
public sealed record ErrorContext(
    string JobId,
    string ChannelId,
    int TicketId,
    string Project,
    int FailedStep,
    int TotalSteps,
    string StepName,
    string RawError,
    string FriendlyError,
    string? LogUrl = null);
