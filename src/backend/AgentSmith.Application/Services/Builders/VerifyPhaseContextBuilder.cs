using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Builders;

/// <summary>
/// p0393: builds the VerifyPhase context from the per-repo project maps AnalyzeCode
/// published. An absent map is not an error here — the handler treats a repo with no
/// declared build/test command as skipped, which is the same outcome.
/// </summary>
public sealed class VerifyPhaseContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ResolvedProject project, PipelineContext pipeline)
    {
        var maps = pipeline.TryGet<IReadOnlyDictionary<string, ProjectMap>>(
            ContextKeys.RepoProjectMaps, out var m) && m is not null
            ? m
            : new Dictionary<string, ProjectMap>();
        return new VerifyPhaseContext(maps, pipeline);
    }
}
