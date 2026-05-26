using AgentSmith.Server.Services;
using Microsoft.AspNetCore.Http.Features;

namespace AgentSmith.Server.Api;

/// <summary>
/// p0169b: GET /api/jobs/{id}/stream — Server-Sent Events stream of the
/// run's progress / tool-call / done / error events. Reuses the bus the
/// chat gateway + MCP server already consume (RedisMessageBus job stream).
/// </summary>
public static class JobStreamEndpoint
{
    private static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(30);

    public static IEndpointRouteBuilder MapJobStreamEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/jobs/{id}/stream",
            async (string id, HttpContext ctx, IJobBusSubscriber subscriber, bool? from_beginning) =>
        {
            ctx.Response.ContentType = "text/event-stream";
            ctx.Response.Headers.CacheControl = "no-cache";
            ctx.Response.Headers["X-Accel-Buffering"] = "no";

            // Disable response buffering so events flush as they arrive.
            var bufferingFeature = ctx.Features.Get<IHttpResponseBodyFeature>();
            bufferingFeature?.DisableBuffering();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ctx.RequestAborted);
            cts.CancelAfter(IdleTimeout);

            var replay = from_beginning == true;
            try
            {
                await foreach (var message in subscriber.SubscribeAsync(id, replay, cts.Token))
                {
                    var formatted = SseEventWriter.Format(message);
                    var bytes = System.Text.Encoding.UTF8.GetBytes(formatted);
                    await ctx.Response.Body.WriteAsync(bytes, cts.Token);
                    await ctx.Response.Body.FlushAsync(cts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                // Idle timeout or client close — graceful exit.
            }
        })
        .WithName("StreamJob")
        .RequireCors(JobsEndpoints.CorsPolicy)
        .ExcludeFromDescription(); // text/event-stream isn't a clean OpenAPI shape; documented separately

        return app;
    }
}
