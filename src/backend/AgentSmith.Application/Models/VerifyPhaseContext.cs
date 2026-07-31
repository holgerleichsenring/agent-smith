using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Models;

/// <summary>
/// p0393: context for the VerifyPhase step — the per-repo project maps whose
/// <see cref="CiConfig"/> carries the build and test commands, plus the pipeline
/// bag the sandboxes are resolved from.
/// </summary>
public sealed record VerifyPhaseContext(
    IReadOnlyDictionary<string, ProjectMap> RepoProjectMaps,
    PipelineContext Pipeline) : ICommandContext;
