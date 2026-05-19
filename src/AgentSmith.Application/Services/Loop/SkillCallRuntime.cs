using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Validation;
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
    private readonly ILogger<SkillCallRuntime> _logger;

    public SkillCallRuntime(
        IChatClientFactory chatFactory,
        PipelineConcurrencyGate gate, LoopLimitsConfig limits,
        OutcomeClassifier classifier, RetryCoordinator retry,
        SkillOutputValidatorFactory validatorFactory,
        RuntimeObservationFactory runtimeObservationFactory,
        ILogger<SkillCallRuntime> logger)
    {
        _chatFactory = chatFactory;
        _gate = gate;
        _limits = limits;
        _classifier = classifier;
        _retry = retry;
        _validatorFactory = validatorFactory;
        _runtimeObservationFactory = runtimeObservationFactory;
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

        var (retryOutcome, exception) = await TryInvokeAsync(request, costTracker, trace, linkedCts.Token);
        scope.Finalize(enforcer);

        var outcome = ClassifyAndLog(request, retryOutcome, exception, enforcer, trace);
        return BuildResult(request, scope, retryOutcome, outcome, exception, trace, enforcer);
    }

    private async Task<(RetryOutcome? Outcome, Exception? Exception)> TryInvokeAsync(
        SkillCallRequest request, PipelineCostTracker costTracker, LoopTraceCollector trace, CancellationToken ct)
    {
        try
        {
            var cap = _limits.ResolveToolCallCap(request.InvestigatorMode);
            var chat = new TracingChatClient(_chatFactory.Create(request.AgentConfig, request.TaskType, maxIterations: cap), trace);
            var options = new ChatOptions { Tools = WrapTools(request.ToolSet, trace) };
            var messages = request.PromptParts.ToList();
            var validator = _validatorFactory.ForSchema(request.OutputSchema);
            var outcome = await _retry.InvokeAsync(chat, messages, options, validator, ct, costTracker.Track);
            return (outcome, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            return (null, ex);
        }
    }

    private static IList<AITool> WrapTools(IEnumerable<AITool> tools, LoopTraceCollector trace) =>
        tools.Select(t => t is AIFunction f ? (AITool)new TracingAIFunction(f, trace) : t).ToList();

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
                : new[] { runtimeObs }
        };
    }
}
