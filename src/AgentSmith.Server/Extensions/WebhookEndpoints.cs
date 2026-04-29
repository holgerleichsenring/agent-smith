using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Services.Webhooks;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// Maps the four platform webhook routes onto Server's WebApplication. The
/// routing is intentionally identical to the pre-p0107 HttpListener-based
/// WebhookListener so behavior is preserved (status codes, response bodies,
/// dispatch path).
/// </summary>
internal static class WebhookEndpoints
{
    internal static WebApplication MapWebhookEndpoints(this WebApplication app)
    {
        app.MapPost("/webhook", (Delegate)HandleWebhookAsync);
        app.MapPost("/webhook/github", (Delegate)HandleWebhookAsync);
        app.MapPost("/webhook/gitlab", (Delegate)HandleWebhookAsync);
        app.MapPost("/webhook/jira", (Delegate)HandleWebhookAsync);
        return app;
    }

    private static async Task<IResult> HandleWebhookAsync(HttpContext ctx)
    {
        var serverContext = ctx.RequestServices.GetRequiredService<ServerContext>();
        var logger = ctx.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger("AgentSmith.Server.Webhook");

        var body = await EndpointHelpers.ReadBodyAsync(ctx.Request);
        var headers = ExtractHeaders(ctx.Request.Headers);
        var path = ctx.Request.Path.Value ?? "/webhook";

        var processor = new WebhookRequestProcessor(ctx.RequestServices, serverContext.ConfigPath, logger);
        var (statusCode, responseBody) = await processor.ProcessAsync(path, body, headers);
        return Results.Text(responseBody, "text/plain", statusCode: statusCode);
    }

    private static Dictionary<string, string> ExtractHeaders(IHeaderDictionary headers)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in headers)
            dict[key] = value.ToString();
        return dict;
    }
}
