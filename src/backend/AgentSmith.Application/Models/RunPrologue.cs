using AgentSmith.Application.Services.Resume;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Models;

/// <summary>
/// p0515: everything a run needs settled BEFORE it announces itself — the configuration it
/// read, the project and repos it resolved, the commands it will execute and the seeded
/// context they will execute against. It exists as one value because the work that produces
/// it is the work that has to be guarded: a failure in it used to escape before
/// RunStarted was published, which left a reserved run queued forever with no reason on it.
/// </summary>
public sealed record RunPrologue(
    AgentSmithConfig Config,
    ResolvedProject Project,
    IReadOnlyList<RepoConnection> Repos,
    IReadOnlyList<string> Commands,
    PipelineContext Pipeline,
    ResumeExecutionPlan? Resume);
