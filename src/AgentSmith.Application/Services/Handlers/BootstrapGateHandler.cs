using AgentSmith.Application.Models;
using AgentSmith.Contracts.Activation;
using AgentSmith.Contracts.Commands;
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
    ILogger<BootstrapGateHandler> logger)
    : ICommandHandler<BootstrapGateContext>
{
    private const string ApiSecurityScan = "api-security-scan";

    private const string FailMessage =
        "Pipeline aborted: project missing .agentsmith/context.yaml or coding-principles.md. " +
        "Run init-project first.";

    public Task<CommandResult> ExecuteAsync(
        BootstrapGateContext context, CancellationToken cancellationToken)
    {
        var concepts = conceptsFactory(context.Pipeline);
        var pipelineName = context.Pipeline.Get<ResolvedPipelineConfig>(ContextKeys.ResolvedPipeline).PipelineName;

        // p0130a / p0102a: api-security-scan has a legitimate passive mode where
        // no source is checked out (schema + live-target probing only). Bootstrap
        // files are inapplicable in that mode. Hardcoded inline (one consumer);
        // a second conditional preset triggers the registry refactor.
        if (string.Equals(pipelineName, ApiSecurityScan, StringComparison.OrdinalIgnoreCase)
            && !concepts.GetBool("source_available"))
        {
            logger.LogDebug("Bootstrap gate skipped: passive api-scan mode (no source resolved).");
            return Task.FromResult(CommandResult.Ok("Bootstrap gate skipped: passive api-scan mode."));
        }

        var contextYamlPresent = concepts.GetBool("context_yaml_present");
        var principlesPresent = concepts.GetBool("coding_principles_present");

        if (!contextYamlPresent || !principlesPresent)
        {
            logger.LogError(
                "Bootstrap gate aborts pipeline: context.yaml={Context}, coding-principles.md={Principles}",
                contextYamlPresent, principlesPresent);
            return Task.FromResult(CommandResult.Fail(FailMessage));
        }

        return Task.FromResult(CommandResult.Ok("Bootstrap files present."));
    }
}
