using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Sandbox;
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
/// Per-context bootstrap probe (p0158f + p0161a). Iterates
/// ContextKeys.Sandboxes keys (each = one discovered context) and asks
/// <see cref="BootstrapContextProbe"/> for context.yaml + coding-principles.md.
/// Publishes:
///   - context_yaml_present : true only if EVERY context has it
///   - coding_principles_present : true only if EVERY context has it
///   - ContextKeys.MissingBootstrapRepos : comma-separated sandbox keys
///     missing either file (consumed by BootstrapGateHandler).
///   - ContextKeys.BootstrapProbeReport : p0496 — the branch, its base and the
///     paths that were read, so a refusal states what it did instead of guessing.
/// </summary>
public sealed class BootstrapCheckHandler(
    BootstrapContextProbe probe,
    SandboxBaseBranch baseBranch,
    Func<PipelineContext, IRunStateConcepts> conceptsFactory,
    SandboxTargets sandboxTargets,
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
        if (!sandboxTargets.TryResolve(context.Pipeline, out var sandboxes, out var discoveries))
            return CommandResult.Fail("BootstrapCheck requires Sandboxes + SandboxDiscoveries.");
        logger.LogInformation(
            "Probe start: {SandboxCount} sandboxes [{Keys}]",
            sandboxes.Count, string.Join(", ", sandboxes.Keys));

        var allContext = true;
        var allPrinciples = true;
        var missing = new List<string>();
        var paths = new List<string>();
        ISandbox? firstMissing = null;
        foreach (var (key, sandbox) in sandboxes)
        {
            var contexts = ContextsIn(context.Pipeline, key, discoveries);
            if (contexts.Count > 0) paths.AddRange(BootstrapContextProbe.PathsFor(contexts));
            var (contextOk, principlesOk) = contexts.Count == 0
                ? NoContexts(key)
                : await probe.ProbeAsync(sandbox, key, contexts, cancellationToken);
            if (!contextOk || !principlesOk)
            {
                missing.Add(key);
                firstMissing ??= sandbox;
            }
            allContext &= contextOk;
            allPrinciples &= principlesOk;
        }

        var concepts = conceptsFactory(context.Pipeline);
        concepts.SetBool("context_yaml_present", allContext);
        concepts.SetBool("coding_principles_present", allPrinciples);
        context.Pipeline.Set(ContextKeys.MissingBootstrapRepos, string.Join(",", missing));
        if (firstMissing is not null)
            context.Pipeline.Set(
                ContextKeys.BootstrapProbeReport,
                await ReportAsync(context.Pipeline, firstMissing, paths, cancellationToken));

        logger.LogInformation(
            "Probe done: context.yaml={Context} principles={Principles} missing=[{Missing}]",
            allContext, allPrinciples, string.Join(", ", missing));
        return CommandResult.Ok($"context.yaml={allContext}, principles={allPrinciples}, missing={missing.Count}");
    }

    private (bool Context, bool Principles) NoContexts(string key)
    {
        logger.LogWarning("Probe {Key}: no context entries. Counted as missing.", key);
        return (false, false);
    }

    // p0180: prefer the per-sandbox context list (one sandbox can hold many contexts when
    // they share a toolchain image); fall back to the representative discovery.
    private static IReadOnlyList<RemoteContextDiscovery> ContextsIn(
        PipelineContext pipeline, string key,
        IReadOnlyDictionary<string, RemoteContextDiscovery> discoveries)
    {
        if (pipeline.TryGet<IReadOnlyDictionary<string, IReadOnlyList<RemoteContextDiscovery>>>(
                ContextKeys.SandboxContexts, out var bySandbox)
            && bySandbox is not null && bySandbox.TryGetValue(key, out var list))
            return list;
        return discoveries.TryGetValue(key, out var discovery) ? [discovery] : [];
    }

    private async Task<BootstrapProbeReport> ReportAsync(
        PipelineContext pipeline, ISandbox sandbox, IReadOnlyList<string> paths, CancellationToken ct) =>
        new(BranchOf(pipeline), await baseBranch.ResolveAsync(sandbox, ct), paths);

    private static string? BranchOf(PipelineContext pipeline)
    {
        if (pipeline.TryGet<Repository>(ContextKeys.Repository, out var repo) && repo is not null)
            return repo.CurrentBranch.Value;
        return pipeline.TryGet<string>(ContextKeys.CheckoutBranch, out var branch)
               && !string.IsNullOrWhiteSpace(branch) ? branch : null;
    }
}
