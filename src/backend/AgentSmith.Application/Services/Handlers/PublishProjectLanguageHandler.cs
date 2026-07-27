using AgentSmith.Application.Models;
using AgentSmith.Contracts.Activation;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Publishes <see cref="ProjectMap.PrimaryLanguage"/> verbatim (trimmed +
/// lower-cased) as the <c>project_language</c> concept. p0155 deletes the
/// closed-enum mapper: the LLM owns the vocabulary via the project-analyzer
/// prompt's slug rule, and BootstrapDispatch fails loud on no-match with the
/// language slug + available skill names. Missing ProjectMap or empty
/// PrimaryLanguage is now a Fail — masking it as "generic" hid missing-data
/// bugs behind a successful classification.
/// </summary>
public sealed class PublishProjectLanguageHandler(
    Func<PipelineContext, IRunStateConcepts> conceptsFactory,
    ILogger<PublishProjectLanguageHandler> logger)
    : ICommandHandler<PublishProjectLanguageContext>, IConceptWriter
{
    public IReadOnlyList<ConceptDeclaration> DeclaredConcepts { get; } =
        [new ConceptDeclaration("project_language", ConceptType.String)];

    public Task<CommandResult> ExecuteAsync(
        PublishProjectLanguageContext context, CancellationToken cancellationToken)
    {
        var perRepo = ResolvePerRepo(context.Pipeline);
        if (perRepo is null || perRepo.Count == 0)
            return Task.FromResult(CommandResult.Fail(
                "PublishProjectLanguage: ContextKeys.RepoProjectMaps is missing. " +
                "AnalyzeProject must run before this step."));

        var slugs = perRepo.Values
            .Select(m => m.PrimaryLanguage?.Trim().ToLowerInvariant() ?? string.Empty)
            .Where(s => !string.IsNullOrEmpty(s))
            .ToList();
        if (slugs.Count == 0)
            return Task.FromResult(CommandResult.Fail(
                "PublishProjectLanguage: ProjectMap.PrimaryLanguage is null or empty. " +
                "The project-analyzer prompt requires a non-empty canonical slug — " +
                "if the analyzer cannot determine one, it should emit 'generic' explicitly."));

        var primary = slugs[0];
        var aggregate = string.Join(",", slugs.Distinct(StringComparer.Ordinal));
        var concepts = conceptsFactory(context.Pipeline);
        concepts.SetString("project_language", primary);
        // project_languages is a new aggregate concept (p0158f); silently skip when
        // the pinned skill catalog hasn't declared it yet (operator updates catalog
        // in a separate step). When supported the concept lights up automatically.
        try { concepts.SetString("project_languages", aggregate); }
        catch (KeyNotFoundException) { /* concept not in current vocab — skip */ }

        logger.LogDebug("Published project_language={Primary}, project_languages={Aggregate}",
            primary, aggregate);
        // Single-repo back-compat: just primary slug in the message.
        return Task.FromResult(perRepo.Count == 1
            ? CommandResult.Ok($"project_language={primary}")
            : CommandResult.Ok($"project_language={primary}, project_languages={aggregate}"));
    }

    // p0384: ContextKeys.RepoProjectMaps is the only analysis surface — a
    // single-repo run is a dictionary of one, same code path.
    private static IReadOnlyDictionary<string, ProjectMap>? ResolvePerRepo(PipelineContext pipeline) =>
        pipeline.TryGet<IReadOnlyDictionary<string, ProjectMap>>(
            ContextKeys.RepoProjectMaps, out var dict) && dict is { Count: > 0 }
            ? dict
            : null;
}
