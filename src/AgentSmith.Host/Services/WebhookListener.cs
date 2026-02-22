using System.Net;
using System.Text.Json;
using AgentSmith.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Host.Services;

/// <summary>
/// Minimal HTTP webhook listener for GitHub/Azure DevOps webhook events.
/// Listens for issue labeled events and triggers Agent Smith runs.
/// </summary>
public sealed class WebhookListener(
    IServiceProvider services,
    string configPath,
    ILogger<WebhookListener> logger)
{
    private const string TriggerLabel = "agent-smith";

    public async Task RunAsync(int port, CancellationToken cancellationToken)
    {
        var prefix = $"http://+:{port}/";
        using var listener = new HttpListener();
        listener.Prefixes.Add(prefix);
        listener.Start();

        logger.LogInformation("Agent Smith webhook listener started on port {Port}", port);
        logger.LogInformation("Waiting for GitHub/Azure DevOps webhook events...");

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var context = await listener.GetContextAsync().WaitAsync(cancellationToken);
                _ = Task.Run(() => HandleRequestAsync(context), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error accepting webhook request");
            }
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
                await RespondAsync(context, 404, "Not found. Use POST /webhook or GET /health");
                return;
            }

            using var reader = new StreamReader(context.Request.InputStream);
            var body = await reader.ReadToEndAsync();

            var triggerInput = TryParseGitHubEvent(body, context.Request.Headers);
            if (triggerInput is null)
            {
                await RespondAsync(context, 200, "Event ignored (not a matching labeled event)");
                return;
            }

            logger.LogInformation("Webhook triggered: {Input}", triggerInput);
            await RespondAsync(context, 202, $"Accepted: {triggerInput}");

            await ExecuteRunAsync(triggerInput);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling webhook request");
            await RespondAsync(context, 500, "Internal server error");
        }
    }

    private string? TryParseGitHubEvent(string body, System.Collections.Specialized.NameValueCollection headers)
    {
        var eventType = headers["X-GitHub-Event"];
        if (eventType != "issues")
            return null;

        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            var action = root.GetProperty("action").GetString();
            if (action != "labeled")
                return null;

            var labelName = root.GetProperty("label").GetProperty("name").GetString();
            if (!string.Equals(labelName, TriggerLabel, StringComparison.OrdinalIgnoreCase))
                return null;

            var issueNumber = root.GetProperty("issue").GetProperty("number").GetInt32();
            var repoName = root.GetProperty("repository").GetProperty("name").GetString();

            return $"fix #{issueNumber} in {repoName}";
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse GitHub webhook payload");
            return null;
        }
    }

    private async Task ExecuteRunAsync(string input)
    {
        try
        {
            var useCase = services.GetRequiredService<ProcessTicketUseCase>();
            var result = await useCase.ExecuteAsync(input, configPath, headless: true);

            if (result.IsSuccess)
                logger.LogInformation("Run completed successfully: {Message}", result.Message);
            else
                logger.LogWarning("Run failed: {Message}", result.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Run failed with exception for input: {Input}", input);
        }
    }

    private static async Task RespondAsync(HttpListenerContext context, int statusCode, string body)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "text/plain";
        var buffer = System.Text.Encoding.UTF8.GetBytes(body);
        context.Response.ContentLength64 = buffer.Length;
        await context.Response.OutputStream.WriteAsync(buffer);
        context.Response.Close();
    }
}
