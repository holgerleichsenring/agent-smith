using System.Net;
using AgentSmith.Application.Services.Health;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Cli.Services;

/// <summary>
/// HTTP server that accepts webhook requests and exposes liveness (/health) and
/// readiness (/health/ready) endpoints. Webhook handling is delegated to
/// <see cref="WebhookRequestProcessor"/>; health responses are built from
/// the injected <see cref="ISubsystemHealth"/> list (p0101).
/// </summary>
public sealed class WebhookListener(
    IServiceProvider services,
    string configPath,
    SubsystemHealth ownHealth,
    IReadOnlyList<ISubsystemHealth> allSubsystems,
    ILogger<WebhookListener> logger)
{
    public async Task RunAsync(int port, CancellationToken cancellationToken)
    {
        var prefix = $"http://+:{port}/";
        using var listener = new HttpListener();
        listener.Prefixes.Add(prefix);
        listener.Start();
        ownHealth.SetUp();
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
        ownHealth.SetDown("listener stopped");
        logger.LogInformation("Webhook listener stopped");
    }

    private async Task HandleRequestAsync(HttpListenerContext context)
    {
        try
        {
            if (TryHandleHealth(context)) return;

            var path = context.Request.Url?.AbsolutePath;
            if (context.Request.HttpMethod != "POST"
                || path is not ("/webhook" or "/webhook/jira" or "/webhook/github" or "/webhook/gitlab"))
            {
                await RespondAsync(context, 404, "Not found", "text/plain");
                return;
            }

            using var reader = new StreamReader(context.Request.InputStream);
            var body = await reader.ReadToEndAsync();
            var headers = ExtractHeaders(context.Request.Headers);

            var processor = new WebhookRequestProcessor(services, configPath, logger);
            var (statusCode, responseBody) = await processor.ProcessAsync(path!, body, headers);
            await RespondAsync(context, statusCode, responseBody, "text/plain");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling webhook");
            await RespondAsync(context, 500, "Internal server error", "text/plain");
        }
    }

    private bool TryHandleHealth(HttpListenerContext context)
    {
        if (context.Request.HttpMethod != "GET") return false;
        var path = context.Request.Url?.AbsolutePath;
        if (path == "/health")
        {
            var (code, body) = HealthResponseBuilder.Liveness(allSubsystems);
            _ = RespondAsync(context, code, body, "application/json");
            return true;
        }
        if (path == "/health/ready")
        {
            var (code, body) = HealthResponseBuilder.Readiness(allSubsystems);
            _ = RespondAsync(context, code, body, "application/json");
            return true;
        }
        return false;
    }

    private static Dictionary<string, string> ExtractHeaders(
        System.Collections.Specialized.NameValueCollection headers)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string? key in headers.AllKeys)
            if (key is not null) dict[key] = headers[key] ?? "";
        return dict;
    }

    private static async Task RespondAsync(
        HttpListenerContext ctx, int code, string body, string contentType)
    {
        ctx.Response.StatusCode = code;
        ctx.Response.ContentType = contentType;
        var buf = System.Text.Encoding.UTF8.GetBytes(body);
        ctx.Response.ContentLength64 = buf.Length;
        await ctx.Response.OutputStream.WriteAsync(buf);
        ctx.Response.Close();
    }
}
