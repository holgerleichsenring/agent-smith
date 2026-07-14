using AgentSmith.Contracts.Expectations;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Expectations;

/// <summary>
/// p0328: default <see cref="IExpectationTrackerCommenter"/>. Resolves the
/// platform template via keyed singleton (TrackerConnection.Type, the
/// PlanOpenQuestionsPoster precedent) and posts via UpdateStatusAsync —
/// comment only, no status transition: the ticket stays in its working status
/// while the run parks on the p0327 checkpoint.
/// </summary>
public sealed class ExpectationTrackerCommenter(
    IServiceProvider services,
    ITicketProviderFactory ticketFactory,
    ILogger<ExpectationTrackerCommenter> logger) : IExpectationTrackerCommenter
{
    public async Task PostAsync(
        TrackerConnection tracker, TicketId ticketId, ExpectationDraft draft,
        CancellationToken cancellationToken)
    {
        try
        {
            var template = ResolveTemplate(tracker.Type.ToString());
            await ticketFactory.Create(tracker)
                .UpdateStatusAsync(ticketId, template.Render(draft), cancellationToken);
            logger.LogInformation(
                "Posted expectation draft on ticket {Ticket} via {Platform}", ticketId, tracker.Type);
        }
        catch (Exception ex)
        {
            // Fail-soft by design: the dialogue transports still carry the
            // ratification ask; the tracker comment is a visibility channel.
            logger.LogWarning(ex,
                "Could not post the expectation draft on ticket {Ticket} ({Platform}) — "
                + "ratification continues via dialogue transports", ticketId, tracker.Type);
        }
    }

    private IExpectationCommentTemplate ResolveTemplate(string platform)
        => services.GetKeyedService<IExpectationCommentTemplate>(platform.ToLowerInvariant())
           ?? throw new InvalidOperationException(
               $"No IExpectationCommentTemplate registered for platform '{platform}'. "
               + "Register one via AddKeyedSingleton<IExpectationCommentTemplate, ...>(\"<platform>\").");
}
