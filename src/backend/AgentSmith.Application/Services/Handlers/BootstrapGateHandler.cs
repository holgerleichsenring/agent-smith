using AgentSmith.Application.Models;
using AgentSmith.Contracts.Activation;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Reads the concepts published by <see cref="BootstrapCheckHandler"/> and aborts
/// the pipeline when bootstrap files are missing. For pipelines that require those
/// files unconditionally (FixBug, FixNoTest, AddFeature, SecurityScan, Autonomous)
/// the gate is strict. For api-security-scan the gate is conditional on
/// <c>source_available</c> — passive runs (schema-only / live-target probing without
/// source checkout, per p0102a) skip the check cleanly.
/// </summary>
public sealed class BootstrapGateHandler(
    Func<PipelineContext, IRunStateConcepts> conceptsFactory,
    IEventPublisher eventPublisher,
    ILogger<BootstrapGateHandler> logger)
    : ICommandHandler<BootstrapGateContext>
{
    private const string GateName = "bootstrap";
    private const string ApiSecurityScan = "api-security-scan";

    public async Task<CommandResult> ExecuteAsync(
        BootstrapGateContext context, CancellationToken cancellationToken)
    {
        var concepts = conceptsFactory(context.Pipeline);
        var pipelineName = context.Pipeline.Get<ResolvedPipelineConfig>(ContextKeys.ResolvedPipeline).PipelineName;
        var runId = context.Pipeline.TryGet<string>(ContextKeys.RunId, out var r) ? r : null;

        if (string.Equals(pipelineName, ApiSecurityScan, StringComparison.OrdinalIgnoreCase)
            && !concepts.GetBool("source_available"))
        {
            await PublishGateAsync(runId, passed: true, "passive api-scan mode", cancellationToken);
            logger.LogDebug("Bootstrap gate skipped: passive api-scan mode (no source resolved).");
            return CommandResult.Ok("Bootstrap gate skipped: passive api-scan mode.");
        }

        var contextYamlPresent = concepts.GetBool("context_yaml_present");
        var principlesPresent = concepts.GetBool("coding_principles_present");

        if (contextYamlPresent && principlesPresent)
        {
            await PublishGateAsync(runId, passed: true, "bootstrap files present", cancellationToken);
            return CommandResult.Ok("Bootstrap files present in every repo.");
        }

        var missingCsv = context.Pipeline.TryGet<string>(
            ContextKeys.MissingBootstrapRepos, out var m) ? m ?? string.Empty : string.Empty;
        var missingList = string.IsNullOrEmpty(missingCsv)
            ? "(unknown — BootstrapCheck did not run)"
            : $"[{missingCsv}]";
        logger.LogError(
            "Bootstrap gate aborts pipeline: missing in repos {Missing} (context.yaml all-present={Context}, principles all-present={Principles})",
            missingList, contextYamlPresent, principlesPresent);
        await PublishGateAsync(runId, passed: false, $"missing in {missingList}", cancellationToken);
        // p0355: name the rename hypothesis — a scoped repo that is unexpectedly
        // empty/new (an operator rename the trigger didn't flag) presents exactly
        // as missing bootstrap, and the operator should check the repo name first.
        return CommandResult.Fail(
            $"Pipeline aborted: missing bootstrap in repos: {missingList}. " +
            "The repo may be empty or newly renamed — verify the scoped repo name. " +
            "Run init-project first.");
    }

    private Task PublishGateAsync(string? runId, bool passed, string reason, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(runId)) return Task.CompletedTask;
        return eventPublisher.PublishAsync(
            new GateCheckedEvent(runId!, GateName, passed, reason, DateTimeOffset.UtcNow), ct);
    }
}
