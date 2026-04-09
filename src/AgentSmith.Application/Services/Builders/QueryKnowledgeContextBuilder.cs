using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Builders;

public sealed class QueryKnowledgeContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ProjectConfig project, PipelineContext pipeline)
    {
        var question = pipeline.Get<string>(ContextKeys.DialogueQuestion);
        var repo = pipeline.Get<Repository>(ContextKeys.Repository);
        var wikiPath = Path.Combine(repo.LocalPath, ".agentsmith", "wiki");
        return new QueryKnowledgeContext(question, wikiPath, pipeline);
    }
}
