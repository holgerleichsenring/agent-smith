using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Builders;

/// <summary>
/// Builds AskContext from the PipelineContext.
/// The DialogQuestion must be pre-set in the pipeline under <see cref="ContextKeys.DialogueQuestion"/>.
/// </summary>
public sealed class AskContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ProjectConfig project, PipelineContext pipeline)
    {
        var question = pipeline.Get<DialogQuestion>(ContextKeys.DialogueQuestion);
        return new AskContext(question, pipeline);
    }
}
