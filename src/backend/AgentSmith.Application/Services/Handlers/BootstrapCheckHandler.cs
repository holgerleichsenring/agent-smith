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
        var contextsBySandbox = context.Pipeline.TryGet<IReadOnlyDictionary<string, IReadOnlyList<RemoteContextDiscovery>>>(
            ContextKeys.SandboxContexts, out var c) && c is not null ? c : null;

        logger.LogInformation(
            "Probe start: {SandboxCount} sandboxes [{Keys}]",
            sandboxes.Count, string.Join(", ", sandboxes.Keys));

        var allContext = true;
        var allPrinciples = true;
        var missing = new List<string>();
        foreach (var (key, sandbox) in sandboxes)
        {
            // p0180: prefer the per-sandbox context list (one sandbox can hold
            // many contexts when they share a toolchain image); fall back to
            // the representative discovery for back-compat.
            IReadOnlyList<RemoteContextDiscovery>? contextsInSandbox = null;
            if (contextsBySandbox is not null && contextsBySandbox.TryGetValue(key, out var list))
                contextsInSandbox = list;
            else if (discoveries.TryGetValue(key, out var discovery))
                contextsInSandbox = new[] { discovery };

            if (contextsInSandbox is null || contextsInSandbox.Count == 0)
            {
                logger.LogWarning(
                    "Probe {Key}: no context entries. Counted as missing.", key);
                missing.Add(key);
                allContext = allPrinciples = false;
                continue;
            }

            var (sandboxCtxOk, sandboxPrincOk) = await ProbeAllContextsAsync(
                sandbox, key, contextsInSandbox, cancellationToken);
            if (!sandboxCtxOk || !sandboxPrincOk) missing.Add(key);
            allContext &= sandboxCtxOk;
            allPrinciples &= sandboxPrincOk;
        }

        var concepts = conceptsFactory(context.Pipeline);
        concepts.SetBool("context_yaml_present", allContext);
        concepts.SetBool("coding_principles_present", allPrinciples);
        context.Pipeline.Set(ContextKeys.MissingBootstrapRepos, string.Join(",", missing));

        logger.LogInformation(
            "Probe done: context.yaml={Context} principles={Principles} missing=[{Missing}]",
            allContext, allPrinciples, string.Join(", ", missing));
        return CommandResult.Ok($"context.yaml={allContext}, principles={allPrinciples}, missing={missing.Count}");
    }

    private async Task<(bool Context, bool Principles)> ProbeAllContextsAsync(
        ISandbox sandbox, string key,
        IReadOnlyList<RemoteContextDiscovery> contextsInSandbox, CancellationToken ct)
    {
        var allCtx = true;
        var allPrinc = true;
        foreach (var discovery in contextsInSandbox)
        {
            var (ctx, princ) = await ProbeOneAsync(sandbox, key, discovery, ct);
            allCtx &= ctx;
            allPrinc &= princ;
        }
        return (allCtx, allPrinc);
    }

    private async Task<(bool Context, bool Principles)> ProbeOneAsync(
        ISandbox sandbox, string key, RemoteContextDiscovery discovery, CancellationToken ct)
    {
        var metaDir = ProjectMetaPaths.MetaDirFor(discovery.ContextName);
        var contextPath = $"{metaDir}/{ProjectMetaPaths.ContextYamlFile}";
        var principlesPath = $"{metaDir}/{ProjectMetaPaths.CodingPrinciplesFile}";
        var reader = readerFactory.Create(sandbox);
        var ctx = await reader.ExistsAsync(contextPath, ct);
        var princ = await reader.ExistsAsync(principlesPath, ct);
        if (ctx && princ)
        {
            logger.LogInformation(
                "Probe {Key}/{Context}: context.yaml=true principles=true",
                key, discovery.ContextName);
        }
        else
        {
            logger.LogInformation(
                "Probe {Key}/{Context}: context.yaml={CtxOk} principles={PrincOk} (path={MetaDir}/)",
                key, discovery.ContextName, ctx, princ, metaDir);
        }
        return (ctx, princ);
    }
}
