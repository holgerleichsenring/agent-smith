using AgentSmith.Application.Models;
using AgentSmith.Contracts.Models;
using AgentSmith.Application.Services;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Cli.Services;

/// <summary>
/// Processes validated webhook requests: detects platform, verifies signature,
/// dispatches to handlers, and routes results. All structured ticket webhooks
/// (GitHub, GitLab, AzureDevOps, Jira) go through ITicketClaimService since p95b.
/// Free-form TriggerInput (PR comments, dialogue answers) stays on the legacy path.
/// </summary>
internal sealed class WebhookRequestProcessor(
    IServiceProvider services,
    string configPath,
    ILogger logger)
{
    public async Task<(int StatusCode, string Body)> ProcessAsync(
        string path, string body, IDictionary<string, string> headers)
    {
        var (platform, eventType) = WebhookPlatformDetector.Detect(path, body, headers);
        if (platform is null)
            return (200, "Unknown platform");

        var verifier = new WebhookSignatureVerifier(services, logger);
        if (!verifier.Validate(platform, body, headers))
        {
            logger.LogWarning("Signature validation failed for {Platform}", platform);
            return (401, "Signature validation failed");
        }

        var handlers = services.GetServices<IWebhookHandler>();
        var result = await DispatchAsync(handlers, platform, eventType!, body, headers);

        if (!result.Handled)
            return (200, "Event ignored");

        if (result.DialogueAnswer is not null)
        {
            var router = new WebhookDialogueRouter(services, logger);
            _ = router.RouteAsync(result.DialogueAnswer);
            return (202, "Accepted: dialogue answer");
        }

        logger.LogInformation("Webhook: {Project}/{Ticket} (pipeline: {Pipeline})",
            result.ProjectName ?? result.TriggerInput, result.TicketId, result.Pipeline ?? "default");

        if (result.ProjectName is not null && result.Platform is not null)
            return await RouteToClaimServiceAsync(result);

        if (result.TriggerInput is not null)
            _ = ExecuteLegacyAsync(result.TriggerInput, result.Pipeline, result.InitialContext);

        return (202, $"Accepted: {result.TriggerInput}");
    }

    private async Task<(int StatusCode, string Body)> RouteToClaimServiceAsync(WebhookResult result)
    {
        var claimService = services.GetRequiredService<ITicketClaimService>();
        var configLoader = services.GetRequiredService<IConfigurationLoader>();
        var config = configLoader.LoadConfig(configPath);

        var claim = new ClaimRequest(
            result.Platform!,
            result.ProjectName!,
            new TicketId(result.TicketId!),
            result.Pipeline ?? "fix-bug",
            result.InitialContext);

        var outcome = await claimService.ClaimAsync(claim, config, CancellationToken.None);
        return MapClaim(outcome, result);
    }

    private static (int, string) MapClaim(ClaimResult outcome, WebhookResult result) => outcome.Outcome switch
    {
        ClaimOutcome.Claimed => (202, $"Accepted: {result.TicketId} in {result.ProjectName}"),
        ClaimOutcome.AlreadyClaimed => (200, $"Already claimed: {result.TicketId}"),
        ClaimOutcome.Rejected => (200, $"Rejected: {outcome.Rejection}"),
        ClaimOutcome.Failed => (500, $"Claim failed: {outcome.Error}"),
        _ => (500, "Unknown claim outcome")
    };

    private static async Task<WebhookResult> DispatchAsync(
        IEnumerable<IWebhookHandler> handlers, string platform, string eventType,
        string body, IDictionary<string, string> headers)
    {
        foreach (var handler in handlers)
        {
            if (!handler.CanHandle(platform, eventType)) continue;
            var result = await handler.HandleAsync(body, headers);
            if (result.Handled) return result;
        }
        return new WebhookResult(false, null, null);
    }

    private async Task ExecuteLegacyAsync(
        string input, string? pipelineOverride, Dictionary<string, object>? initialContext = null)
    {
        try
        {
            var useCase = services.GetRequiredService<ExecutePipelineUseCase>();
            var result = await useCase.ExecuteAsync(
                input, configPath, headless: true, pipelineOverride, CancellationToken.None, initialContext);
            logger.LogInformation(result.IsSuccess
                ? "Run completed: {Message}" : "Run failed: {Message}", result.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Run failed for: {Input}", input);
        }
    }
}
