using AgentSmith.Application.Services;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Cli.Services;

/// <summary>
/// Processes validated webhook requests: detects platform, verifies
/// signature, dispatches to handlers, and routes results.
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

        logger.LogInformation("Webhook: {Input} (pipeline: {Pipeline})",
            result.TriggerInput, result.Pipeline ?? "default");

        if (result.TriggerInput is not null)
            _ = ExecuteRunAsync(result.TriggerInput, result.Pipeline);

        return (202, $"Accepted: {result.TriggerInput}");
    }

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

    private async Task ExecuteRunAsync(string input, string? pipelineOverride)
    {
        try
        {
            var useCase = services.GetRequiredService<ExecutePipelineUseCase>();
            var result = await useCase.ExecuteAsync(
                input, configPath, headless: true, pipelineOverride, CancellationToken.None);
            logger.LogInformation(result.IsSuccess
                ? "Run completed: {Message}" : "Run failed: {Message}", result.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Run failed for: {Input}", input);
        }
    }
}
