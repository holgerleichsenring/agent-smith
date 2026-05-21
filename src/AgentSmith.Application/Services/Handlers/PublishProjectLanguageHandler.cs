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
        if (!context.Pipeline.TryGet<ProjectMap>(ContextKeys.ProjectMap, out var map) || map is null)
            return Task.FromResult(CommandResult.Fail(
                "PublishProjectLanguage: ContextKeys.ProjectMap is missing. " +
                "AnalyzeProject must run before this step."));

        var primaryLanguage = map.PrimaryLanguage?.Trim().ToLowerInvariant() ?? string.Empty;
        if (string.IsNullOrEmpty(primaryLanguage))
            return Task.FromResult(CommandResult.Fail(
                "PublishProjectLanguage: ProjectMap.PrimaryLanguage is null or empty. " +
                "The project-analyzer prompt requires a non-empty canonical slug — " +
                "if the analyzer cannot determine one, it should emit 'generic' explicitly."));

        var concepts = conceptsFactory(context.Pipeline);
        concepts.SetString("project_language", primaryLanguage);

        logger.LogDebug("Published project_language={Language}", primaryLanguage);
        return Task.FromResult(CommandResult.Ok($"project_language={primaryLanguage}"));
    }
}
