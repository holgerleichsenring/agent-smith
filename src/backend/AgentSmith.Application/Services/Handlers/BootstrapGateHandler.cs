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

        if (contextYamlPresent && principlesPresent)
            return Task.FromResult(CommandResult.Ok("Bootstrap files present in every repo."));

        var missingCsv = context.Pipeline.TryGet<string>(
            ContextKeys.MissingBootstrapRepos, out var m) ? m ?? string.Empty : string.Empty;
        var missingList = string.IsNullOrEmpty(missingCsv)
            ? "(unknown — BootstrapCheck did not run)"
            : $"[{missingCsv}]";
        logger.LogError(
            "Bootstrap gate aborts pipeline: missing in repos {Missing} (context.yaml all-present={Context}, principles all-present={Principles})",
            missingList, contextYamlPresent, principlesPresent);
        return Task.FromResult(CommandResult.Fail(
            $"Pipeline aborted: missing bootstrap in repos: {missingList}. " +
            "Run init-project first."));
    }
}
