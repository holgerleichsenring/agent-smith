using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Checks out a source repository from the configured provider.
/// </summary>
public sealed class CheckoutSourceHandler(
    ISourceProviderFactory factory,
    ILogger<CheckoutSourceHandler> logger)
    : ICommandHandler<CheckoutSourceContext>
{
    public async Task<CommandResult> ExecuteAsync(
        CheckoutSourceContext context, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Checking out branch {Branch}...", context.Branch);

        var provider = factory.Create(context.Config);
        var repo = await provider.CheckoutAsync(context.Branch, cancellationToken);

        context.Pipeline.Set(ContextKeys.Repository, repo);
        return CommandResult.Ok($"Repository checked out to {repo.LocalPath}");
    }
}
