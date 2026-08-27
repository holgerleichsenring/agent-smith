using AgentSmith.Server.Services.Http;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// 2026-08-27-7098: wires the answer an unhandled failure gets. The middleware goes on
/// FIRST, ahead of routing, CORS and authentication, because it can only answer for what
/// runs inside it — an exception raised in a handler mapped later still surfaces here, but
/// one raised in a middleware registered earlier would not.
/// </summary>
internal static class FailureReasonExtensions
{
    internal static IServiceCollection AddFailureReasons(this IServiceCollection services)
    {
        services.AddExceptionHandler<UnhandledFailureAnswer>();
        // The framework's fallback for anything the handler above declines to answer.
        services.AddProblemDetails();
        return services;
    }

    internal static WebApplication UseFailureReasons(this WebApplication app)
    {
        app.UseExceptionHandler();
        return app;
    }
}
