using System.Net;
using AgentSmith.Application.Services;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Services;
using AgentSmith.Cli.Services.Webhooks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Cli.Services;

/// <summary>
/// Thin HTTP server that dispatches webhook events to IWebhookHandler implementations.
/// Determines platform from headers, validates signature, finds matching handler.
/// Routes dialogue answers to waiting agent jobs via IDialogueTransport.
/// </summary>
public sealed class WebhookListener(
    IServiceProvider services,
    string configPath,
    ILogger<WebhookListener> logger)
{
    public async Task RunAsync(int port, CancellationToken cancellationToken)
    {
        var prefix = $"http://+:{port}/";
        using var listener = new HttpListener();
        listener.Prefixes.Add(prefix);
        listener.Start();

        logger.LogInformation("Webhook listener started on port {Port}", port);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var context = await listener.GetContextAsync().WaitAsync(cancellationToken);
                _ = Task.Run(() => HandleRequestAsync(context), cancellationToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { logger.LogError(ex, "Error accepting webhook request"); }
        }

        listener.Stop();
        logger.LogInformation("Webhook listener stopped");
    }

    private async Task HandleRequestAsync(HttpListenerContext context)
    {
        try
        {
            if (context.Request.HttpMethod == "GET" && context.Request.Url?.AbsolutePath == "/health")
            {
                await RespondAsync(context, 200, "ok");
                return;
            }

            if (context.Request.HttpMethod != "POST" || context.Request.Url?.AbsolutePath != "/webhook")
            {
                await RespondAsync(context, 404, "Not found");
                return;
            }

            using var reader = new StreamReader(context.Request.InputStream);
            var body = await reader.ReadToEndAsync();
            var headers = ExtractHeaders(context.Request.Headers);
            var (platform, eventType) = DetectPlatform(headers);

            if (platform is null)
            {
                await RespondAsync(context, 200, "Unknown platform");
                return;
            }

            if (!ValidateSignature(platform, body, headers))
            {
                logger.LogWarning("Signature validation failed for {Platform}", platform);
                await RespondAsync(context, 401, "Signature validation failed");
                return;
            }

            var handlers = services.GetServices<IWebhookHandler>();
            var result = await DispatchAsync(handlers, platform, eventType!, body, headers);

            if (!result.Handled)
            {
                await RespondAsync(context, 200, "Event ignored");
                return;
            }

            if (result.DialogueAnswer is not null)
            {
                logger.LogInformation("Webhook: dialogue answer from {Author} on {Repo}#{Pr}",
                    result.DialogueAnswer.AuthorLogin, result.DialogueAnswer.RepoFullName,
                    result.DialogueAnswer.PrIdentifier);
                await RespondAsync(context, 202, "Accepted: dialogue answer");
                await HandleDialogueAnswerAsync(result.DialogueAnswer);
                return;
            }

            logger.LogInformation("Webhook: {Input} (pipeline: {Pipeline})",
                result.TriggerInput, result.Pipeline ?? "default");
            await RespondAsync(context, 202, $"Accepted: {result.TriggerInput}");

            if (result.TriggerInput is not null)
                await ExecuteRunAsync(result.TriggerInput, result.Pipeline);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling webhook");
            await RespondAsync(context, 500, "Internal server error");
        }
    }

    private static async Task<WebhookResult> DispatchAsync(
        IEnumerable<IWebhookHandler> handlers, string platform, string eventType,
        string body, IDictionary<string, string> headers)
    {
        foreach (var handler in handlers)
        {
            if (!handler.CanHandle(platform, eventType))
                continue;
            var result = await handler.HandleAsync(body, headers);
            if (result.Handled) return result;
        }
        return new WebhookResult(false, null, null);
    }

    private static (string? platform, string? eventType) DetectPlatform(
        IDictionary<string, string> headers)
    {
        if (headers.TryGetValue("X-GitHub-Event", out var ghEvent))
            return ("github", ghEvent);
        if (headers.TryGetValue("X-Gitlab-Event", out var glEvent))
            return ("gitlab", glEvent.Contains("Merge Request") ? "merge_request" : glEvent.ToLowerInvariant());
        if (headers.TryGetValue("X-Azure-DevOps-EventType", out var azdoEvent))
            return ("azuredevops", azdoEvent);
        return (null, null);
    }

    private static bool ValidateSignature(
        string platform, string body, IDictionary<string, string> headers)
    {
        return platform switch
        {
            "github" => !headers.TryGetValue("X-Hub-Signature-256", out var sig)
                        || WebhookSignatureValidator.ValidateGitHub(body, sig,
                            Environment.GetEnvironmentVariable("GITHUB_WEBHOOK_SECRET") ?? ""),
            "gitlab" => !headers.TryGetValue("X-Gitlab-Token", out var token)
                        || WebhookSignatureValidator.ValidateGitLab(token,
                            Environment.GetEnvironmentVariable("GITLAB_WEBHOOK_TOKEN") ?? ""),
            "azuredevops" => !headers.TryGetValue("Authorization", out var auth)
                        || WebhookSignatureValidator.ValidateAzureDevOps(auth,
                            Environment.GetEnvironmentVariable("AZDO_WEBHOOK_SECRET") ?? ""),
            _ => true
        };
    }

    private async Task HandleDialogueAnswerAsync(DialogueAnswerData data)
    {
        try
        {
            var lookup = services.GetService<IConversationLookup>();
            var transport = services.GetService<IDialogueTransport>();

            if (lookup is null || transport is null)
            {
                logger.LogWarning(
                    "Dialogue answer received but IConversationLookup or IDialogueTransport not registered");
                return;
            }

            var conversation = await lookup.FindByPrAsync(
                data.Platform, data.RepoFullName, data.PrIdentifier, CancellationToken.None);

            if (conversation is null)
            {
                logger.LogWarning(
                    "No active job found for PR {Repo}#{Pr} — dialogue answer ignored",
                    data.RepoFullName, data.PrIdentifier);
                return;
            }

            if (conversation.PendingQuestionId is null)
            {
                logger.LogWarning(
                    "Job {JobId} has no pending question — dialogue answer ignored",
                    conversation.JobId);
                return;
            }

            var answer = new DialogAnswer(
                QuestionId: conversation.PendingQuestionId,
                Answer: data.Answer,
                Comment: data.Comment,
                AnsweredAt: DateTimeOffset.UtcNow,
                AnsweredBy: data.AuthorLogin);

            await transport.PublishAnswerAsync(conversation.JobId, answer, CancellationToken.None);

            logger.LogInformation(
                "Published dialogue answer ({Answer}) for job {JobId}, question {QuestionId}",
                data.Answer, conversation.JobId, conversation.PendingQuestionId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to handle dialogue answer for {Repo}#{Pr}",
                data.RepoFullName, data.PrIdentifier);
        }
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

    private static Dictionary<string, string> ExtractHeaders(
        System.Collections.Specialized.NameValueCollection headers)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string? key in headers.AllKeys)
            if (key is not null) dict[key] = headers[key] ?? "";
        return dict;
    }

    private static async Task RespondAsync(HttpListenerContext ctx, int code, string body)
    {
        ctx.Response.StatusCode = code;
        ctx.Response.ContentType = "text/plain";
        var buf = System.Text.Encoding.UTF8.GetBytes(body);
        ctx.Response.ContentLength64 = buf.Length;
        await ctx.Response.OutputStream.WriteAsync(buf);
        ctx.Response.Close();
    }
}
