namespace SampleApi.Middleware;

public sealed class XFrameOptionsMiddleware
{
    private readonly RequestDelegate _next;
    public XFrameOptionsMiddleware(RequestDelegate next) => _next = next;

    public async Task Invoke(HttpContext ctx)
    {
        ctx.Response.Headers.Append("X-Frame-Options", "DENY");
        // NOTE: no Strict-Transport-Security, no Content-Security-Policy, no X-Content-Type-Options
        await _next(ctx);
    }
}
