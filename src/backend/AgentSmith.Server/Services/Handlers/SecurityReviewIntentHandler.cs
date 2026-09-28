using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.ChatLaunch;

namespace AgentSmith.Server.Services.Handlers;

/// <summary>
/// Handles the SecurityReviewIntent: a ticketless security-scan run of the project, or of one
/// pull request when the command names one — then the run carries the pull request's context,
/// so it scans the pull request's head rather than the default branch.
/// </summary>
public sealed class SecurityReviewIntentHandler(
    IConfigurationLoader configLoader,
    ServerContext serverContext,
    ChatPrContextResolver prContext,
    ChatTicketlessRunLauncher launcher,
    ChatLaunchAnnouncer announcer)
{
    public const string Pipeline = "security-scan";

    public async Task HandleAsync(SecurityReviewIntent intent, CancellationToken cancellationToken)
    {
        var subject = intent.PrIdentifier is { } pr
            ? $"a security review of PR #{pr} in *{intent.Project}*"
            : $"a security review of *{intent.Project}*";
        var result = await LaunchAsync(intent, cancellationToken);
        await announcer.AnnounceAsync(intent.ChannelId, subject, result, cancellationToken);
    }

    private async Task<ChatLaunchResult> LaunchAsync(SecurityReviewIntent intent, CancellationToken ct)
    {
        var config = configLoader.LoadConfig(serverContext.ConfigPath);
        if (!config.Projects.TryGetValue(intent.Project, out var project))
            return ChatLaunchResult.Refused($"Project '{intent.Project}' is not configured.");
        if (intent.PrIdentifier is not { } pr)
            return await launcher.LaunchAsync(project, Pipeline, [], ct);

        if (prContext.PullRequestRepo(project) is not { } repo)
            return ChatLaunchResult.Refused(
                $"Project '{project.Name}' does not have exactly one repo with pull requests, so "
                + $"PR #{pr} does not say which repo it is in. On GitHub and GitLab a label on the "
                + "pull request starts the scan on its own repo.");
        var context = await prContext.ResolveAsync(repo, pr, ct);
        return await launcher.LaunchAsync(project, Pipeline, context, ct);
    }
}
