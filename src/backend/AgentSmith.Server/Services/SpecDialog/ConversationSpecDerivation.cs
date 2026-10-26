using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Services.Derivation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-10-26-bdd0g: a spec derivation of the bound ticket, asked for in its conversation by the turn's poster. The
/// launch runs in a scope of its own, apart from the turn's unit of work; its refusals are said as they are.
/// </summary>
public sealed class ConversationSpecDerivation(
    IServiceScopeFactory scopes, ResolvedProject project, string ticketId, string conversation, string poster,
    ILogger<ConversationSpecDerivation> logger) : ISpecDerivationStart
{
    public async Task<string> StartAsync(bool preview, string reason, CancellationToken cancellationToken)
    {
        logger.LogInformation("Spec derivation of {Project}/#{Ticket} asked by {Poster} in conversation {Conversation}: {Reason}",
            project.Name, ticketId, poster, conversation, reason);
        await using var scope = scopes.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<SpecDerivationLauncher>()
            .LaunchAsync(project.Name, ticketId, preview, cancellationToken);
        return result.Outcome == SpecDerivationOutcome.Started
            ? $"started as run {result.RunId}; the derived specs appear on the ticket page when it ends"
            : $"Not started: {result.Reason}";
    }
}
