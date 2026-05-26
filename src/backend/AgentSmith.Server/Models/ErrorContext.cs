using AgentSmith.Infrastructure.Models;
using AgentSmith.Server.Services;

namespace AgentSmith.Server.Models;

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
    string? LogUrl)
{
    public static ErrorContext FromBusMessage(ConversationState state, BusMessage message) =>
        new(
            JobId: message.JobId,
            ChannelId: state.ChannelId,
            TicketId: state.TicketId,
            Project: state.Project,
            FailedStep: message.Step ?? 0,
            TotalSteps: message.Total ?? 0,
            StepName: message.StepName ?? string.Empty,
            RawError: message.Text,
            FriendlyError: ErrorFormatter.Humanize(message.Text),
            LogUrl: null);
}
