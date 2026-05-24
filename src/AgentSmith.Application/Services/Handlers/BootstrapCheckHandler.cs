using AgentSmith.Application.Models;
using AgentSmith.Contracts.Activation;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Per-context bootstrap probe (p0158f + p0161a). Iterates
/// ContextKeys.Sandboxes keys (each = one discovered context); for each key
/// uses ContextKeys.SandboxDiscoveries to derive the per-context MetaDir
/// `/work/.agentsmith/contexts/&lt;contextName&gt;` and probes for
/// context.yaml + coding-principles.md. Publishes:
///   - context_yaml_present : true only if EVERY context has it
///   - coding_principles_present : true only if EVERY context has it
///   - ContextKeys.MissingBootstrapRepos : comma-separated sandbox keys
///     missing either file (consumed by BootstrapGateHandler).
/// </summary>
public sealed class BootstrapCheckHandler(
    ISandboxFileReaderFactory readerFactory,
    Func<PipelineContext, IRunStateConcepts> conceptsFactory,
    ILogger<BootstrapCheckHandler> logger)
    : ICommandHandler<BootstrapCheckContext>, IConceptWriter
{
    public IReadOnlyList<ConceptDeclaration> DeclaredConcepts { get; } =
    [
        new ConceptDeclaration("context_yaml_present", ConceptType.Bool),
        new ConceptDeclaration("coding_principles_present", ConceptType.Bool)
    ];

    public async Task<CommandResult> ExecuteAsync(
        BootstrapCheckContext context, CancellationToken cancellationToken)
    {
        if (!SandboxTargets.TryResolve(context.Pipeline, out var sandboxes, out var discoveries))
            return CommandResult.Fail("BootstrapCheck requires Sandboxes + SandboxDiscoveries.");

        var allContext = true;
        var allPrinciples = true;
        var missing = new List<string>();
        foreach (var (key, sandbox) in sandboxes)
        {
            if (!discoveries.TryGetValue(key, out var discovery))
            {
                missing.Add(key);
                allContext = allPrinciples = false;
                continue;
            }
            var (ctx, princ) = await ProbeOneAsync(sandbox, key, discovery, cancellationToken);
            if (!ctx || !princ) missing.Add(key);
            allContext &= ctx;
            allPrinciples &= princ;
        }

        var concepts = conceptsFactory(context.Pipeline);
        concepts.SetBool("context_yaml_present", allContext);
        concepts.SetBool("coding_principles_present", allPrinciples);
        context.Pipeline.Set(ContextKeys.MissingBootstrapRepos, string.Join(",", missing));

        logger.LogDebug(
            "Bootstrap probe: context.yaml all-present={Context}, coding-principles all-present={Principles}, missing=[{Missing}]",
            allContext, allPrinciples, string.Join(", ", missing));
        return CommandResult.Ok($"context.yaml={allContext}, principles={allPrinciples}, missing={missing.Count}");
    }

    private async Task<(bool Context, bool Principles)> ProbeOneAsync(
        ISandbox sandbox, string key, RemoteContextDiscovery discovery, CancellationToken ct)
    {
        var metaDir = ProjectMetaPaths.MetaDirFor(discovery.ContextName);
        var reader = readerFactory.Create(sandbox);
        var ctx = await reader.ExistsAsync($"{metaDir}/{ProjectMetaPaths.ContextYamlFile}", ct);
        var princ = await reader.ExistsAsync($"{metaDir}/{ProjectMetaPaths.CodingPrinciplesFile}", ct);
        logger.LogDebug("{Key} ({Ctx}): context.yaml={CtxOk}, principles={PrincOk}",
            key, discovery.ContextName, ctx, princ);
        return (ctx, princ);
    }
}
