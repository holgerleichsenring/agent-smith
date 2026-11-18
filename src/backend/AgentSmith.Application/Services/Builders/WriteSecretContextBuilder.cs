using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Builders;

/// <summary>2026-11-17-f3a8d: builder for the WriteSecret step.</summary>
public sealed class WriteSecretContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ResolvedProject project, PipelineContext pipeline) =>
        new WriteSecretContext(pipeline, project);
}
