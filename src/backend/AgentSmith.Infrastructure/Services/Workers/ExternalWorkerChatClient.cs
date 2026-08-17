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
        var prompt = renderer.Render(request);
        var result = await runner.RunAsync(prompt, options, cancellationToken);
        logger.LogInformation(
            "External worker answered {Request} in {Seconds:F1}s (exit {Exit}, prompt {Chars} chars)",
            request.Describe(), result.Duration.TotalSeconds, result.ExitCode, prompt.Length);

        WorkerProcessGuard.RequireUsable(request, result, prompt.Length);
        // p0426: SILENCE is a round, not a rupture. A worker that exits 0 having written
        // nothing is a measured, recurring behaviour of this transport — and run 27 threw
        // away eleven minutes of verified work over one of them. An empty turn is a shape
        // the loop already knows how to answer (its re-engage nudge), and its iteration
        // and time limits stay the ceiling, so this cannot spin forever.
        if (string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            logger.LogWarning(
                "External worker {Request} answered nothing on a {Chars:N0}-char prompt — "
                + "surfacing an empty turn for the loop to nudge on.",
                request.Describe(), prompt.Length);
            return WorkerReplyTranslator.EmptyTurn(request);
        }

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

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException(
            "The external-worker bridge answers whole calls; streaming has no worker counterpart.");

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() { }
}
