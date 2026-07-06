using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Triage;

/// <summary>
/// Default <see cref="IPlanOpenQuestionsPoster"/>. Selects the platform-specific
/// comment template via keyed singleton (TrackerConnection.Type) and posts the rendered
/// body via ITicketProvider.UpdateStatusAsync. Posts on the ticket itself, not on
/// a PR, since the Plan phase precedes the PR.
/// </summary>
public sealed class PlanOpenQuestionsPoster : IPlanOpenQuestionsPoster
{
    private readonly IServiceProvider _services;
    private readonly ITicketProviderFactory _ticketFactory;
    private readonly ILogger<PlanOpenQuestionsPoster> _logger;

    public PlanOpenQuestionsPoster(
        IServiceProvider services,
        ITicketProviderFactory ticketFactory,
        ILogger<PlanOpenQuestionsPoster> logger)
    {
        _services = services;
        _ticketFactory = ticketFactory;
        _logger = logger;
    }

    public async Task PostAsync(
        TrackerConnection ticketConfig, TicketId ticketId,
        IReadOnlyList<PlanOpenQuestion> questions, string? parkStatus, CancellationToken cancellationToken)
    {
        if (questions.Count == 0)
        {
            _logger.LogDebug("No open questions to post for ticket {Ticket}", ticketId);
            return;
        }

        var template = ResolveTemplate(ticketConfig.Type.ToString());
        var body = template.Render(questions);
        var provider = _ticketFactory.Create(ticketConfig);

        // p0318: with a park status, post the comment AND move the native status in ONE
        // provider call (atomic on AzDO — avoids the TF26071 rev race). Without it, only
        // the comment is posted so the ticket stays claimable (no persistent park).
        if (string.IsNullOrWhiteSpace(parkStatus))
            await provider.UpdateStatusAsync(ticketId, body, cancellationToken);
        else
            await provider.FinalizeAsync(ticketId, body, parkStatus, cancellationToken);

        _logger.LogInformation(
            "Posted {Count} open question(s) on ticket {Ticket} via {Platform}{Parked}",
            questions.Count, ticketId, ticketConfig.Type,
            string.IsNullOrWhiteSpace(parkStatus) ? "" : $" (parked -> {parkStatus})");
    }

    private ITicketCommentTemplate ResolveTemplate(string platform)
        => _services.GetKeyedService<ITicketCommentTemplate>(platform.ToLowerInvariant())
           ?? throw new InvalidOperationException(
               $"No ITicketCommentTemplate registered for platform '{platform}'. " +
               "Register one via AddKeyedSingleton<ITicketCommentTemplate, ...>(\"<platform>\").");
}
