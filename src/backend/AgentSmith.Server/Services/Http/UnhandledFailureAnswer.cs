using AgentSmith.Server.Models;
using Microsoft.AspNetCore.Diagnostics;

namespace AgentSmith.Server.Services.Http;

/// <summary>
/// 2026-08-27-7098: answers a failure nothing else answered, with the reason it failed for.
/// The server declares a reason on every outcome it planned for; what it had no answer for
/// was the outcome it did not plan for, and that is precisely the one an operator needs
/// named. Registered once at the composition root, so every bodyless 500 on every route
/// gains a reason — not only the route whose button reported one.
/// <para>
/// It reports the exception's own message rather than a fixed sentence: this is a
/// self-hosted operator tool whose findings surface already quotes the causes it catches,
/// and a generic "an error occurred" is the empty body again with extra steps.
/// </para>
/// </summary>
internal sealed class UnhandledFailureAnswer(ILogger<UnhandledFailureAnswer> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        logger.LogError(
            exception, "Unhandled failure answering {Method} {Path}",
            httpContext.Request.Method, httpContext.Request.Path);

        // A response already on the wire cannot be replaced by one carrying a reason, and
        // a caller that hung up has nobody left to read it.
        if (httpContext.Response.HasStarted || cancellationToken.IsCancellationRequested)
        {
            logger.LogDebug("The failure could not be answered — the response had already left");
            return false;
        }

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(
            new FailureReasonResponse(Reason(exception)), cancellationToken);
        return true;
    }

    /// <summary>The exception as an operator quotes it: the type, because the message alone
    /// is sometimes only "Object reference not set", and the message, because it is usually
    /// the whole answer.</summary>
    internal static string Reason(Exception exception) =>
        string.IsNullOrWhiteSpace(exception.Message)
            ? exception.GetType().Name
            : $"{exception.GetType().Name}: {exception.Message.Trim()}";
}
