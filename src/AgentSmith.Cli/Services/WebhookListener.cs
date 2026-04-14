using System.Net;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Cli.Services;

/// <summary>
/// Thin HTTP server that accepts webhook requests and delegates
/// processing to <see cref="WebhookRequestProcessor"/>.
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

            var path = context.Request.Url?.AbsolutePath;
            if (context.Request.HttpMethod != "POST"
                || path is not ("/webhook" or "/webhook/jira" or "/webhook/github" or "/webhook/gitlab"))
            {
                await RespondAsync(context, 404, "Not found");
                return;
            }

            using var reader = new StreamReader(context.Request.InputStream);
            var body = await reader.ReadToEndAsync();
            var headers = ExtractHeaders(context.Request.Headers);

            var processor = new WebhookRequestProcessor(services, configPath, logger);
            var (statusCode, responseBody) = await processor.ProcessAsync(path!, body, headers);
            await RespondAsync(context, statusCode, responseBody);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling webhook");
            await RespondAsync(context, 500, "Internal server error");
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
