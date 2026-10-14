using Microsoft.Extensions.AI;

namespace AgentSmith.Infrastructure.Services.Factories;

/// <summary>
/// How a tool loop treats a tool that throws, declared by this product rather than inherited
/// from Microsoft.Extensions.AI. Every factory that builds a tool loop applies it, so a live,
/// a replayed and a scripted run fail at the same call.
/// </summary>
public static class ToolLoopErrorTolerance
{
    /// <summary>
    /// Consecutive failing tool iterations a loop survives; the next one rethrows and ends the
    /// caller's loop as a failure. Three answers "is this loop stuck", not "how long may a broken
    /// tool keep failing": a model that has read the same failure three times in a row and still
    /// calls into it is not converging, and a tool whose failure is recoverable says so in its
    /// result instead of throwing.
    /// </summary>
    public const int ConsecutiveErrorsPerRequest = 3;

    /// <summary>
    /// The model reads the failed tool's exception message, not a bare "the function failed".
    /// A model that cannot see why a call failed can only reword it. The message adds no reader:
    /// tool results already carry raw command output and file contents to the same provider,
    /// web_fetch and ensure_repo_sandbox already return the exception message as their result,
    /// and the one tool whose result is a secret hands it over on success and is scrubbed from
    /// history by call id whether it failed or not.
    /// </summary>
    public const bool DetailedErrors = true;

    public static T Apply<T>(T loop) where T : FunctionInvokingChatClient
    {
        loop.MaximumConsecutiveErrorsPerRequest = ConsecutiveErrorsPerRequest;
        loop.IncludeDetailedErrors = DetailedErrors;
        return loop;
    }
}
