using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Builders;

/// <summary>2026-11-17-0207d: builder for the VerifySeries step.</summary>
public sealed class VerifySeriesContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ResolvedProject project, PipelineContext pipeline) =>
        new VerifySeriesContext(pipeline, project);
}
