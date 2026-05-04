using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Builders;

public sealed class FilterRoundContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ProjectConfig project, PipelineContext pipeline)
    {
        var skillName = command.SkillName ?? string.Empty;
        var round = command.Round ?? 1;
        return new FilterRoundContext(skillName, round, pipeline.Resolved().Agent, pipeline);
    }
}
