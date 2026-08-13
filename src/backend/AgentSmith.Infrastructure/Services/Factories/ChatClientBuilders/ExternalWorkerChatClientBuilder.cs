using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Services.Workers;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders;

/// <summary>
/// p0416: builds the chat client an EXTERNAL agent CLI answers. Selection is the agent's
/// declared <c>type: external_worker</c> and nothing else — the builder is registered but
/// never chosen unless an operator wrote that type into a configured agent, so no
/// production path can fall into worker mode by accident or by a missing credential.
/// </summary>
public sealed class ExternalWorkerChatClientBuilder(
    WorkerRequestComposer composer,
    WorkerPromptRenderer renderer,
    WorkerReplyParser parser,
    WorkerReplyTranslator translator,
    IWorkerProcessRunner runner,
    IRunContextAccessor runContext,
    ExternalWorkerCliOptionsFactory optionsFactory,
    ILoggerFactory loggerFactory) : IChatClientBuilder
{
    public const string TypeName = "external_worker";

    public IReadOnlyList<string> SupportedTypes { get; } = [TypeName];

    public IChatClient Build(AgentConfig agent, ModelAssignment assignment) =>
        new ExternalWorkerChatClient(
            composer, renderer, parser, translator, runner, runContext,
            optionsFactory.Create(agent, assignment),
            TypeName,
            assignment.Model,
            loggerFactory.CreateLogger<ExternalWorkerChatClient>());
}
