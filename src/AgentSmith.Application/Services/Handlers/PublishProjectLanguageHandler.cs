using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Activation;
using AgentSmith.Contracts.Activation;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Reads <see cref="ProjectMap.PrimaryLanguage"/> from <c>ContextKeys.ProjectMap</c>,
/// translates it through <see cref="ProjectLanguageMapper"/> into the closed
/// <c>project_language</c> enum, and publishes via <see cref="IRunStateConcepts"/>.
/// Missing ProjectMap is non-fatal — defaults to <c>generic</c> so the pipeline
/// can still pick the fallback bootstrap skill.
/// </summary>
public sealed class PublishProjectLanguageHandler(
    Func<PipelineContext, IRunStateConcepts> conceptsFactory,
    ILogger<PublishProjectLanguageHandler> logger)
    : ICommandHandler<PublishProjectLanguageContext>, IConceptWriter
{
    public IReadOnlyList<ConceptDeclaration> DeclaredConcepts { get; } =
        [new ConceptDeclaration("project_language", ConceptType.Enum)];

    public Task<CommandResult> ExecuteAsync(
        PublishProjectLanguageContext context, CancellationToken cancellationToken)
    {
        var primaryLanguage = context.Pipeline.TryGet<ProjectMap>(ContextKeys.ProjectMap, out var map)
            && map is not null
            ? map.PrimaryLanguage
            : null;

        var mapped = ProjectLanguageMapper.Map(primaryLanguage);
        var concepts = conceptsFactory(context.Pipeline);
        concepts.SetEnum("project_language", mapped);

        logger.LogDebug(
            "Published project_language={Mapped} (from PrimaryLanguage={Primary})",
            mapped, primaryLanguage ?? "<none>");
        return Task.FromResult(CommandResult.Ok($"project_language={mapped}"));
    }
}
