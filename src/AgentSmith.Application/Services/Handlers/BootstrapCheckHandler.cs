using AgentSmith.Application.Models;
using AgentSmith.Contracts.Activation;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Probes the working repo for <c>.agentsmith/context.yaml</c> and
/// <c>.agentsmith/coding-principles.md</c> via <see cref="ISandboxFileReader"/>
/// and publishes <c>context_yaml_present</c> + <c>coding_principles_present</c>.
/// Registered in DI but not wired into any pipeline preset in p0125c — gating
/// (the policy decision of whether to abort on missing files) lives next to
/// init-project's bootstrap flow in p0130 (D6).
/// </summary>
public sealed class BootstrapCheckHandler(
    ISandboxFileReaderFactory readerFactory,
    Func<PipelineContext, IRunStateConcepts> conceptsFactory,
    ILogger<BootstrapCheckHandler> logger)
    : ICommandHandler<BootstrapCheckContext>, IConceptWriter
{
    private const string ContextYamlPath = $"{Repository.SandboxWorkPath}/{ProjectMetaPaths.ContextYaml}";
    private const string CodingPrinciplesPath = $"{Repository.SandboxWorkPath}/{ProjectMetaPaths.CodingPrinciples}";

    public IReadOnlyList<ConceptDeclaration> DeclaredConcepts { get; } =
    [
        new ConceptDeclaration("context_yaml_present", ConceptType.Bool),
        new ConceptDeclaration("coding_principles_present", ConceptType.Bool)
    ];

    public async Task<CommandResult> ExecuteAsync(
        BootstrapCheckContext context, CancellationToken cancellationToken)
    {
        if (!context.Pipeline.TryGet<ISandbox>(ContextKeys.Sandbox, out var sandbox) || sandbox is null)
            return CommandResult.Fail("BootstrapCheck requires an active sandbox.");

        var reader = readerFactory.Create(sandbox);
        var contextYamlPresent = await reader.ExistsAsync(ContextYamlPath, cancellationToken);
        var principlesPresent = await reader.ExistsAsync(CodingPrinciplesPath, cancellationToken);

        var concepts = conceptsFactory(context.Pipeline);
        concepts.SetBool("context_yaml_present", contextYamlPresent);
        concepts.SetBool("coding_principles_present", principlesPresent);

        logger.LogDebug(
            "Bootstrap files: context.yaml={Context}, coding-principles.md={Principles}",
            contextYamlPresent, principlesPresent);
        return CommandResult.Ok($"context.yaml={contextYamlPresent}, principles={principlesPresent}");
    }
}
