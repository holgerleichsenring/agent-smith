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
    private readonly ILogger<SkillCallRuntime> _logger;

    public SkillCallRuntime(
        IChatClientFactory chatFactory,
        PipelineConcurrencyGate gate, LoopLimitsConfig limits,
        OutcomeClassifier classifier, RetryCoordinator retry,
        SkillOutputValidatorFactory validatorFactory,
        RuntimeObservationFactory runtimeObservationFactory,
        IEventPublisher eventPublisher,
        IRunContextAccessor runContext,
        ILogger<SkillCallRuntime> logger)
    {
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
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var enforcer = new LimitEnforcer(_limits, linkedCts);
        var trace = new LoopTraceCollector();

        if (costTracker.IsBudgetExhausted)
        {
            scope.Finalize(enforcer);
            return BuildCostCapExhaustedResult(request, scope, trace, enforcer, costTracker);
        }

        var (retryOutcome, exception) = await TryInvokeAsync(request, costTracker, trace, linkedCts.Token);
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
            var inner = _chatFactory.Create(request.AgentConfig, request.TaskType, maxIterations: cap);
            // EventPublishing wraps innermost (below the retry layer) so every
            // provider attempt produces its own LlmCallStarted/Finished pair
            // with the actual response's token counts — not an aggregated total.
            var instrumented = new EventPublishingChatClient(inner, _eventPublisher, _runContext, request.Role);
            var chat = new TracingChatClient(instrumented, trace);
            var options = new ChatOptions { Tools = WrapTools(request.ToolSet, trace) };
            var messages = request.PromptParts.ToList();
            var validator = _validatorFactory.ForSchema(request.OutputSchema);

            LogPromptIfEnabled(request, messages, options);

            var outcome = await _retry.InvokeAsync(chat, messages, options, validator, ct, costTracker.Track);
            return (outcome, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            return (null, ex);
        }
    }

    /// <summary>
    /// Debug-level dump of the exact prompt + tool surface handed to the LLM for this
    /// skill call. Off by default (Debug); enable via appsettings / Logging:LogLevel:
    /// <c>"AgentSmith.Application.Services.Loop.SkillCallRuntime": "Debug"</c> when
    /// diagnosing skill prompt-composition issues. Each chat message is dumped in full
    /// with its role + char count; tool names + descriptions are listed.
    /// </summary>
    private void LogPromptIfEnabled(SkillCallRequest request, IList<ChatMessage> messages, ChatOptions options)
    {
        if (!_logger.IsEnabled(LogLevel.Debug)) return;
        for (var i = 0; i < messages.Count; i++)
        {
            var msg = messages[i];
            var text = string.Join("\n", msg.Contents.OfType<TextContent>().Select(t => t.Text));
            _logger.LogDebug(
                "skill_prompt skill={Skill} msg[{Index}/{Total}] role={Role} chars={Chars}\n{Text}",
                request.SkillName, i + 1, messages.Count, msg.Role, text.Length, text);
        }
        var toolNames = options.Tools?.OfType<AIFunction>().Select(t => t.Name).ToList() ?? new();
        _logger.LogDebug(
            "skill_prompt skill={Skill} tools_offered={Count} names=[{Names}]",
            request.SkillName, toolNames.Count, string.Join(", ", toolNames));
    }

    private IList<AITool> WrapTools(IEnumerable<AITool> tools, LoopTraceCollector trace) =>
        tools.Select(t => t is AIFunction f ? Wrap(f, trace) : t).ToList();

    private AITool Wrap(AIFunction f, LoopTraceCollector trace)
    {
        var withEvents = new EventPublishingAIFunction(f, _eventPublisher, _runContext);
        return new TracingAIFunction(withEvents, trace);
    }

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
        var failureReason = retryOutcome?.FailureReason ?? exception?.Message;
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
}
