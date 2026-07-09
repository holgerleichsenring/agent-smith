using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Activation;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Application.Services.SkillRounds;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Activation;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services;

/// <summary>
/// Default <see cref="IVerifyRoundCoordinator"/>. Resolves active VerifyDiff
/// investigators via <see cref="ActivationSkillFilter"/>, builds the
/// Verify-phase tool surface from the pipeline sandbox, dispatches each
/// verifier through <see cref="ISkillCallRuntime"/>, and parses observations
/// with confidence-downgrade. Runtime observations (execution-limit /
/// execution-error) are buffered so silent verifier drops still surface.
/// </summary>
public sealed class VerifyRoundCoordinator(
    ActivationSkillFilter activationFilter,
    ISkillBodyResolver bodyResolver,
    Func<PipelineContext, IRunStateConcepts> conceptsFactory,
    IDecisionLogger decisionLogger,
    IToolKit toolKit,
    ISkillCallRuntime skillCallRuntime,
    ISkillResponseParser responseParser,
    ISkillRoundBufferDispatcher bufferDispatcher,
    ILogger<VerifyRoundCoordinator> logger) : IVerifyRoundCoordinator
{
    public async Task<VerifyRoundResult> RunRoundAsync(
        string planJson, string diffJson, AgentConfig agentConfig,
        PipelineContext pipeline, CancellationToken cancellationToken)
    {
        var verifiers = ResolveActiveVerifiers(pipeline);
        if (verifiers.Count == 0)
        {
            logger.LogInformation("Verify phase: no active VerifyDiff investigators");
            return new VerifyRoundResult(0, []);
        }
        var tools = ResolveVerifyTools(pipeline);
        var combined = new List<SkillObservation>();
        foreach (var verifier in verifiers)
            combined.AddRange(await InvokeAsync(
                verifier, planJson, diffJson, agentConfig, tools, pipeline, cancellationToken));
        return new VerifyRoundResult(verifiers.Count, combined);
    }

    private IReadOnlyList<RoleSkillDefinition> ResolveActiveVerifiers(PipelineContext pipeline)
    {
        var roles = pipeline.TryGet<IReadOnlyList<RoleSkillDefinition>>(
            ContextKeys.AvailableRoles, out var r) && r is not null ? r : [];
        var verifiers = roles
            .Where(x => string.Equals(x.Role, "investigator", StringComparison.OrdinalIgnoreCase)
                     && string.Equals(x.InvestigatorMode, "verify_diff", StringComparison.OrdinalIgnoreCase))
            .ToList();
        return activationFilter.Filter(verifiers, conceptsFactory(pipeline));
    }

    private IList<AITool> ResolveVerifyTools(PipelineContext pipeline)
    {
        if (!pipeline.TryGet<ISandbox>(ContextKeys.Sandbox, out var sandbox) || sandbox is null)
            return new List<AITool>();
        var pipelineName = pipeline.TryGet<string>(ContextKeys.PipelineName, out var pn) && pn is not null
            ? pn : IToolKit.WildcardPipelineName;
        var hosts = new IToolHost[]
        {
            new FilesystemToolHost(sandbox),
            new LogDecisionToolHost(decisionLogger),
            new HumanToolHost(),
        };
        return toolKit.GetToolsFor(pipelineName, SkillExecutionPhase.Verify, "verify_diff", hosts);
    }

    private async Task<List<SkillObservation>> InvokeAsync(
        RoleSkillDefinition verifier, string planJson, string diffJson,
        AgentConfig agent, IList<AITool> tools,
        PipelineContext pipeline, CancellationToken cancellationToken)
    {
        var body = bodyResolver.ResolveBody(verifier, SkillRole.Analyst);
        var codingPrinciples = pipeline.TryGet<string>(ContextKeys.CodingPrinciples, out var cp) ? cp : null;
        var (system, user) = VerifierPromptBuilder.Build(body, planJson, diffJson, codingPrinciples);
        var request = new SkillCallRequest
        {
            SkillName = verifier.Name,
            Role = verifier.Role ?? "investigator",
            Phase = SkillExecutionPhase.Verify,
            InvestigatorMode = "verify_diff",
            PromptParts = [new(ChatRole.System, system), new(ChatRole.User, user)],
            ToolSet = tools.ToList(),
            AgentConfig = agent,
            TaskType = TaskType.Primary,
            PipelineName = pipeline.TryGet<string>(ContextKeys.PipelineName, out var pn) ? pn : null,
        };
        var result = await skillCallRuntime.ExecuteAsync(
            request, PipelineCostTracker.GetOrCreate(pipeline), cancellationToken);
        if (result.RuntimeObservations.Count > 0)
            bufferDispatcher.Dispatch(pipeline,
                new SkillRoundBuffer(verifier.Name, 0, result.RuntimeObservations.ToList(), null, null));
        if (result.Outcome is not SkillCallOutcome.Ok and not SkillCallOutcome.Incomplete)
        {
            logger.LogWarning("Verifier {Name}: {Outcome} ({Reason})",
                verifier.Name, result.Outcome, result.FailureReason ?? "no reason");
            return [];
        }
        if (result.Outcome == SkillCallOutcome.Incomplete)
            logger.LogWarning("Verifier {Name} returned Incomplete (limit: {Limit})",
                verifier.Name, result.Cost.HitLimit ?? "unknown");
        return responseParser.ParseAndDowngrade(
            result.Output ?? string.Empty, verifier.Name, logger, result.ReadPaths,
            ResolveConfidenceThreshold(pipeline));
    }

    private static int ResolveConfidenceThreshold(PipelineContext pipeline) =>
        pipeline.TryGet<ResolvedPipelineConfig>(ContextKeys.ResolvedPipeline, out var resolved)
            && resolved is not null
            ? resolved.ConfidenceThreshold
            : ResolvedPipelineConfig.DefaultConfidenceThreshold;
}
