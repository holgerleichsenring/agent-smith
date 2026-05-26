using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.Models;

/// <summary>
/// Context for the PublishProjectLanguage step. The handler reads
/// <c>ContextKeys.ProjectMap</c> (populated by AnalyzeProjectHandler) and
/// publishes the typed <c>project_language</c> enum concept via
/// <see cref="AgentSmith.Contracts.Activation.IRunStateConcepts"/>.
/// </summary>
public sealed record PublishProjectLanguageContext(PipelineContext Pipeline) : ICommandContext;
