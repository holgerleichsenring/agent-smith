using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Workers;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Workers;

/// <summary>
/// p0416: the IChatClient an EXTERNAL agent answers. It sits exactly where a provider
/// client sits — below the rate limiter, the event publisher and the function-invoking
/// loop — so a worker-driven run exercises the whole machinery (master loop, ledger,
/// nudges, acceptance gate, keystone) at zero provider cost.
/// <para>
/// The worker enters as the MODEL: it is shown the request and answers with text or tool
/// calls. Every way that can go wrong — a dead CLI, a timeout, an unparseable answer,
/// an invented tool — throws <see cref="ExternalWorkerCallException"/> naming the run and
/// the step. Nothing degrades silently into an empty response.
/// </para>
/// </summary>
public sealed class ExternalWorkerChatClient(
    WorkerRequestComposer composer,
    WorkerPromptRenderer renderer,
    WorkerReplyParser parser,
    WorkerReplyTranslator translator,
    IWorkerProcessRunner runner,
    IRunContextAccessor runContext,
    ExternalWorkerCliOptions options,
    string agentType,
    string model,
    ILogger logger) : IChatClient
{
    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? chatOptions = null,
        CancellationToken cancellationToken = default)
    {
        var request = composer.Compose(messages, chatOptions, Identity());
        var result = await runner.RunAsync(renderer.Render(request), options, cancellationToken);
        logger.LogInformation(
            "External worker answered {Request} in {Seconds:F1}s (exit {Exit})",
            request.Describe(), result.Duration.TotalSeconds, result.ExitCode);

        RequireUsableProcess(request, result);
        if (!parser.TryParse(result.StandardOutput, out var reply, out var parseProblem))
            throw new ExternalWorkerCallException(request, parseProblem!, result.Duration);
        if (!translator.TryTranslate(reply, request, out var response, out var replyProblem))
            throw new ExternalWorkerCallException(request, replyProblem!, result.Duration);
        return response;
    }

    private WorkerCallIdentity Identity()
    {
        var scope = runContext.CurrentCallScope;
        return new WorkerCallIdentity(
            runContext.CurrentRunId, runContext.CurrentStepIndex,
            scope?.Role, scope?.Phase, scope?.RepoName, agentType, model);
    }

    private static void RequireUsableProcess(WorkerRequest request, WorkerProcessResult result)
    {
        if (result.TimedOut)
            throw new ExternalWorkerCallException(
                request, "the worker did not answer within the per-call timeout", result.Duration);
        if (result.ExitCode != 0)
            throw new ExternalWorkerCallException(
                request, $"the worker CLI exited with {result.ExitCode}: {Tail(result.StandardError)}",
                result.Duration);
    }

    private const int StandardErrorTail = 500;

    private static string Tail(string stderr)
    {
        var text = stderr.Trim();
        if (text.Length == 0) return "(no stderr)";
        return text.Length <= StandardErrorTail ? text : "…" + text[^StandardErrorTail..];
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException(
            "The external-worker bridge answers whole calls; streaming has no worker counterpart.");

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() { }
}
