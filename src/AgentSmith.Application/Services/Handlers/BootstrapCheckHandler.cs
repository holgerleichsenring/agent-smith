using AgentSmith.Application.Models;
using AgentSmith.Contracts.Activation;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Per-repo bootstrap probe (p0158f). Iterates ContextKeys.Repos; for each
/// repo's sandbox checks /work/.agentsmith/context.yaml and
/// /work/.agentsmith/coding-principles.md. Publishes:
///   - context_yaml_present : true only if EVERY repo has it
///   - coding_principles_present : true only if EVERY repo has it
///   - ContextKeys.MissingBootstrapRepos : comma-separated names of repos
///     missing either file (consumed by BootstrapGateHandler for the
///     operator-facing error message)
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
        var (sandboxes, repos) = ResolveTargets(context.Pipeline);
        if (sandboxes is null || repos is null)
            return CommandResult.Fail("BootstrapCheck requires Sandboxes + Repos (or legacy Sandbox).");

        var allContext = true;
        var allPrinciples = true;
        var missing = new List<string>();
        foreach (var repo in repos)
        {
            if (!sandboxes.TryGetValue(repo.Name, out var sandbox))
            {
                missing.Add(repo.Name);
                allContext = allPrinciples = false;
                continue;
            }
            var (ctx, princ) = await ProbeOneAsync(sandbox, repo.Name, cancellationToken);
            if (!ctx || !princ) missing.Add(repo.Name);
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

    private static (IReadOnlyDictionary<string, ISandbox>?, IReadOnlyList<RepoConnection>?) ResolveTargets(
        PipelineContext pipeline) => MultiRepoTargets.Resolve(pipeline);

    private async Task<(bool Context, bool Principles)> ProbeOneAsync(
        ISandbox sandbox, string repoName, CancellationToken ct)
    {
        var reader = readerFactory.Create(sandbox);
        var ctx = await reader.ExistsAsync(ContextYamlPath, ct);
        var princ = await reader.ExistsAsync(CodingPrinciplesPath, ct);
        logger.LogDebug("{Repo}: context.yaml={Ctx}, principles={Princ}", repoName, ctx, princ);
        return (ctx, princ);
    }
}
