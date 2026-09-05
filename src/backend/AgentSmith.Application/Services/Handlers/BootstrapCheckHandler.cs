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
/// <see cref="BootstrapContextProbe"/> for context.yaml + principles.md.
/// Publishes:
///   - context_yaml_present : true only if EVERY context has it
///   - coding_principles_present : true only if EVERY context has it
///   - ContextKeys.MissingBootstrapRepos : comma-separated sandbox keys
///     missing either file (consumed by BootstrapGateHandler).
///   - ContextKeys.BootstrapProbeReport : p0496 — the branch, its base and the
///     paths that were read, so a refusal states what it did instead of guessing;
///     2026-09-04-ae3a — plus which context lacks which file.
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
        var missingFiles = new List<MissingBootstrapFile>();
        ISandbox? firstMissing = null;
        foreach (var (key, sandbox) in sandboxes)
        {
            var contexts = SandboxContextList.InOr(
                context.Pipeline, key, discoveries.GetValueOrDefault(key));
            if (contexts.Count > 0) paths.AddRange(BootstrapContextProbe.PathsFor(contexts));
            var probed = contexts.Count == 0
                ? NoContexts(key)
                : await probe.ProbeAsync(sandbox, key, contexts, cancellationToken);
            if (probed.Any(r => !r.Complete))
            {
                missing.Add(key);
                firstMissing ??= sandbox;
                // 2026-09-04-ae3a: which context lacks which file, so a repository whose
                // FIRST context is fine is not told the whole repository predates a rename.
                missingFiles.AddRange(probed.SelectMany(r => r.Missing()));
            }
            allContext &= probed.All(r => r.ContextYaml);
            allPrinciples &= probed.All(r => r.Principles);
        }

        var concepts = conceptsFactory(context.Pipeline);
        concepts.SetBool("context_yaml_present", allContext);
        concepts.SetBool("coding_principles_present", allPrinciples);
        context.Pipeline.Set(ContextKeys.MissingBootstrapRepos, string.Join(",", missing));
        if (firstMissing is not null)
            context.Pipeline.Set(
                ContextKeys.BootstrapProbeReport,
                await ReportAsync(context.Pipeline, firstMissing, paths, missingFiles, cancellationToken));

        logger.LogInformation(
            "Probe done: context.yaml={Context} principles={Principles} missing=[{Missing}]",
            allContext, allPrinciples, string.Join(", ", missing));
        return CommandResult.Ok($"context.yaml={allContext}, principles={allPrinciples}, missing={missing.Count}");
    }

    private IReadOnlyList<ContextProbeResult> NoContexts(string key)
    {
        logger.LogWarning("Probe {Key}: no context entries. Counted as missing.", key);
        return [new ContextProbeResult(key, ContextYaml: false, Principles: false, RetiredPrinciples: false)];
    }

    private async Task<BootstrapProbeReport> ReportAsync(
        PipelineContext pipeline, ISandbox sandbox, IReadOnlyList<string> paths,
        IReadOnlyList<MissingBootstrapFile> missing, CancellationToken ct) =>
        new(BranchOf(pipeline), await baseBranch.ResolveAsync(sandbox, ct), paths, missing);

    private static string? BranchOf(PipelineContext pipeline)
    {
        if (pipeline.TryGet<Repository>(ContextKeys.Repository, out var repo) && repo is not null)
            return repo.CurrentBranch.Value;
        return pipeline.TryGet<string>(ContextKeys.CheckoutBranch, out var branch)
               && !string.IsNullOrWhiteSpace(branch) ? branch : null;
    }
}
