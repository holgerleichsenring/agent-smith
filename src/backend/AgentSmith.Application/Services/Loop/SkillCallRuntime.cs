using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Events;
using AgentSmith.Application.Services.Validation;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Loop;

/// <summary>
/// Composes the five collaborator services (LimitEnforcer, RetryCoordinator,
/// LoopTraceCollector, OutcomeClassifier, PipelineConcurrencyGate) into the
/// public ExecuteAsync flow. Constructed per pipeline run via DI; the body
/// is sequencing — all substantive work lives in the collaborators.
/// </summary>
public sealed class SkillCallRuntime : ISkillCallRuntime
{
    private readonly IChatClientFactory _chatFactory;
    private readonly PipelineConcurrencyGate _gate;
    private readonly LoopLimitsConfig _limits;
    private readonly OutcomeClassifier _classifier;
    private readonly RetryCoordinator _retry;
    private readonly SkillOutputValidatorFactory _validatorFactory;
    private readonly RuntimeObservationFactory _runtimeObservationFactory;
    private readonly IEventPublisher _eventPublisher;
    private readonly IRunContextAccessor _runContext;
    private readonly ResultBoundReporter _resultBound;
    private readonly Contracts.Runs.IRunTraceWriter _trace;
    private readonly SkillPromptLogger _promptLogger;
    private readonly ILogger<SkillCallRuntime> _logger;

    public SkillCallRuntime(
        IChatClientFactory chatFactory,
        PipelineConcurrencyGate gate, LoopLimitsConfig limits,
        OutcomeClassifier classifier, RetryCoordinator retry,
        SkillOutputValidatorFactory validatorFactory,
        RuntimeObservationFactory runtimeObservationFactory,
        IEventPublisher eventPublisher,
        IRunContextAccessor runContext,
        ResultBoundReporter resultBound,
        Contracts.Runs.IRunTraceWriter trace,
        SkillPromptLogger promptLogger,
        ILogger<SkillCallRuntime> logger)
    {
        _resultBound = resultBound;
        _trace = trace;
        _promptLogger = promptLogger;
        _chatFactory = chatFactory;
        _gate = gate;
        _limits = limits;
        _classifier = classifier;
        _retry = retry;
        _validatorFactory = validatorFactory;
        _runtimeObservationFactory = runtimeObservationFactory;
        _eventPublisher = eventPublisher;
        _runContext = runContext;
        _logger = logger;
    }

    public async Task<SkillCallResult> ExecuteAsync(
        SkillCallRequest request, PipelineCostTracker costTracker, CancellationToken cancellationToken)
    {
        using var permit = await _gate.AcquireAsync(cancellationToken);
        using var scope = costTracker.BeginCall(request.SkillName, request.Role, request.Phase);
        using var _callScope = _runContext.BeginCallScope(request.Role, request.Phase.ToString());
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var enforcer = new LimitEnforcer(_limits, linkedCts);
        var trace = new LoopTraceCollector();

        if (costTracker.IsBudgetExhausted)
        {
            scope.Finalize(enforcer);
            return BuildCostCapExhaustedResult(request, scope, trace, enforcer, costTracker);
        }

        var (retryOutcome, exception) = await TryInvokeAsync(request, costTracker, trace, linkedCts.Token);
        // p0361: the ambient CallScope counted exact-repeat tool invocations
        // during the call; copy the count so it lands in the CallCostRecord.
        scope.SetDuplicateToolCalls(_runContext.CurrentCallScope?.DuplicateToolCallCount ?? 0);
        scope.Finalize(enforcer);

        var outcome = ClassifyAndLog(request, retryOutcome, exception, enforcer, trace);
        return BuildResult(request, scope, retryOutcome, outcome, exception, trace, enforcer);
    }

    private SkillCallResult BuildCostCapExhaustedResult(
        SkillCallRequest request, SkillCallScope scope, LoopTraceCollector trace,
        LimitEnforcer enforcer, PipelineCostTracker costTracker)
    {
        var totalTokens = (long)costTracker.TotalInputTokens + costTracker.TotalOutputTokens
            + costTracker.TotalCacheCreateTokens + costTracker.TotalCacheReadTokens;
        var observation = _runtimeObservationFactory.BuildCostCapExhausted(
            request.SkillName, costTracker.EstimateCostUsd(), totalTokens);
        _logger.LogWarning(
            "Skill {Skill} skipped — pipeline cost cap exhausted ({Usd:F4} USD / {Tokens} tokens).",
            request.SkillName, costTracker.EstimateCostUsd(), totalTokens);
        return new SkillCallResult
        {
            Outcome = SkillCallOutcome.Incomplete,
            Output = null,
            Cost = scope.BuildRecord(enforcer),
            Trace = trace.Build(),
            FailureReason = "cost cap exhausted",
            RuntimeObservations = new[] { observation },
            ReadPaths = Array.Empty<string>(),
        };
    }

    private async Task<(RetryOutcome? Outcome, Exception? Exception)> TryInvokeAsync(
        SkillCallRequest request, PipelineCostTracker costTracker, LoopTraceCollector trace, CancellationToken ct)
    {
        try
        {
            var cap = _limits.ResolveToolCallCap(request.InvestigatorMode);
            // p0176b: ChatClientFactory now wraps the innermost client with
            // EventPublishingChatClient unconditionally, so every direct
            // consumer (not just this site) emits LlmCallStarted/Finished
            // events. Manual `new EventPublishingChatClient(...)` removed.
            var inner = _chatFactory.Create(request.AgentConfig, request.TaskType, maxIterations: cap);
            var chat = new TracingChatClient(Recorded(inner), trace);
            var options = new ChatOptions { Tools = WrapTools(request.ToolSet, trace) };
            var messages = request.PromptParts.ToList();
            var validator = _validatorFactory.ForSchema(request.OutputSchema);

            _promptLogger.Log(request, messages, options);

            var outcome = await _retry.InvokeAsync(chat, messages, options, validator, ct, costTracker.Track);
            return (outcome, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            return (null, ex);
        }
    }

    // p0423: a traced run keeps the conversation itself. Off by default — the wrapper is
    // not inserted at all, so an untraced run pays nothing.
    private IChatClient Recorded(IChatClient inner) => _trace.IsEnabled
        ? new Trace.RecordingChatClient(inner, _trace, _runContext)
        : inner;

    private AIFunction Recorded(AIFunction inner) => _trace.IsEnabled
        ? new Trace.RecordingAIFunction(inner, _trace, _runContext)
        : inner;

    // p0422: bounded FIRST, so the trace records what the model actually received.
    // p0423: the bound reports the size it cut from, so the event can carry both.
    private IList<AITool> WrapTools(IEnumerable<AITool> tools, LoopTraceCollector trace) =>
        [.. tools.Select(t => t is AIFunction f
            ? Recorded(new TracingAIFunction(
                new EventPublishingAIFunction(
                    new BoundedResultAIFunction(f, _resultBound), _eventPublisher, _runContext, _resultBound),
                trace))
            : t)];

    private SkillCallOutcome ClassifyAndLog(
        SkillCallRequest request, RetryOutcome? retryOutcome, Exception? exception,
        LimitEnforcer enforcer, LoopTraceCollector trace)
    {
        var outcome = _classifier.Classify(BuildClassificationInput(retryOutcome, exception, enforcer));
        trace.EmitLog(_logger, request.SkillName);
        return outcome;
    }

    private static ClassificationInput BuildClassificationInput(
        RetryOutcome? retryOutcome, Exception? exception, LimitEnforcer enforcer)
    {
        var responsePresent = retryOutcome?.FinalOutput is not null;
        var parseSuccess = retryOutcome?.Kind != RetryOutcomeKind.ParseFailedAfterRetry;
        var validationSuccess = retryOutcome?.Kind != RetryOutcomeKind.ValidationFailedAfterRetry;
        var limitHit = !enforcer.CheckTimeLimit()
            ? LimitDecision.Cap(LimitDecisionKind.CappedTime, $"elapsed {enforcer.ElapsedMs}ms")
            : null;

        return new ClassificationInput
        {
            ResponsePresent = responsePresent,
            ParseSuccess = parseSuccess,
            ValidationSuccess = validationSuccess,
            CaughtException = exception,
            LimitHit = limitHit
        };
    }

    private SkillCallResult BuildResult(
        SkillCallRequest request, SkillCallScope scope, RetryOutcome? retryOutcome,
        SkillCallOutcome outcome, Exception? exception, LoopTraceCollector trace,
        LimitEnforcer enforcer)
    {
        var failureReason = retryOutcome?.FailureReason ?? DescribeException(exception);
        var runtimeObs = _runtimeObservationFactory.Build(
            outcome, request.SkillName, enforcer.HitLimit, exception, failureReason);
        return new SkillCallResult
        {
            Outcome = outcome,
            Output = retryOutcome?.FinalOutput,
            Cost = scope.BuildRecord(enforcer),
            Trace = trace.Build(),
            FailureReason = failureReason,
            RuntimeObservations = runtimeObs is null
                ? []
                : new[] { runtimeObs },
            ReadPaths = trace.ReadSet.ToArray()
        };
    }

    // p0236: an OperationCanceledException reaching BuildResult is NEVER an
    // operator/limit/watchdog cancel — those leave the run token cancelled and
    // TryInvokeAsync rethrows them (its `when` clause), so they bubble to the
    // pipeline-level p0232 handler instead. What lands here is an INTERNAL
    // timeout: the LLM SDK's per-request NetworkTimeout fired (the hidden 100s
    // default before p0235). Its raw .Message is the useless ".NET "A task was
    // cancelled." — replace it with the actual lever so the operator (and we)
    // stop guessing.
    private static string? DescribeException(Exception? exception)
    {
        if (exception is null) return null;
        if (exception is OperationCanceledException)
            return "LLM request timed out at the HTTP layer (SDK NetworkTimeout) — "
                + "raise the agent's network_timeout_seconds (default 300s since p0235; "
                + "was a hidden 100s before). Not an operator/budget cancel.";
        return $"{exception.GetType().Name}: {exception.Message}";
    }
}
